using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk;

namespace Data8.PowerPlatform.Dataverse.Client
{
    class ProxySerializationSurrogate :
#if NETCOREAPP
        ISerializationSurrogateProvider
#else
        IDataContractSurrogate
#endif
    {
        private Dictionary<string, Type> _requestTypes;
        private Dictionary<string, Type> _responseTypes;
        private Dictionary<string, Type> _entityTypes;

        public ProxySerializationSurrogate()
        {
        }

        public void LoadAssembly(Assembly assembly)
        {
            _requestTypes = new Dictionary<string, Type>();
            _responseTypes = new Dictionary<string, Type>();
            _entityTypes = new Dictionary<string, Type>();

            foreach (var type in assembly.GetExportedTypes())
            {
                if (typeof(OrganizationRequest).IsAssignableFrom(type))
                {
                    var attr = type.GetCustomAttribute<RequestProxyAttribute>();

                    if (attr != null)
                        _requestTypes[attr.Name] = type;
                }
                else if (typeof(OrganizationResponse).IsAssignableFrom(type))
                {
                    var attr = type.GetCustomAttribute<ResponseProxyAttribute>();

                    if (attr != null)
                        _responseTypes[attr.Name] = type;
                }
                else if (typeof(Entity).IsAssignableFrom(type))
                {
                    var attr = type.GetCustomAttribute<EntityLogicalNameAttribute>();

                    if (attr != null)
                        _entityTypes[attr.LogicalName] = type;
                }
            }
        }

#if NETCOREAPP
        Type ISerializationSurrogateProvider.GetSurrogateType(Type type)
#else
        Type IDataContractSurrogate.GetDataContractType(Type type)
#endif
        {
            if (_entityTypes == null)
                return type;

            if (typeof(OrganizationRequest).IsAssignableFrom(type))
                return typeof(OrganizationRequest);

            if (typeof(OrganizationResponse).IsAssignableFrom(type))
                return typeof(OrganizationResponse);

            if (typeof(Entity).IsAssignableFrom(type))
                return typeof(Entity);

            return type;
        }

        public object GetDeserializedObject(object obj, Type targetType)
        {
            if (_entityTypes == null)
                return obj;

            if (obj is OrganizationRequest req)
            {
                var type = GetRequestType(req.RequestName);
                if (type == null)
                    return obj;

                var converted = (OrganizationRequest)Activator.CreateInstance(type);
                converted.RequestName = req.RequestName;
                converted.RequestId = req.RequestId;
                converted.Parameters = req.Parameters;
                return converted;
            }
            else if (obj is OrganizationResponse resp)
            {
                var type = GetResponseType(resp.ResponseName);
                if (type == null)
                    return obj;

                var converted = (OrganizationResponse)Activator.CreateInstance(type);
                converted.ResponseName = resp.ResponseName;
                converted.Results = resp.Results;
                return converted;
            }
            else if (obj is Entity entity)
            {
                var type = GetEntityType(entity.LogicalName);
                if (type == null)
                    return obj;

                var method = typeof(Entity).GetMethod(nameof(entity.ToEntity)).MakeGenericMethod(type);
                return method.Invoke(entity, Array.Empty<object>());
            }

            return obj;
        }

        public object GetObjectToSerialize(object obj, Type targetType)
        {
            if (_entityTypes == null)
                return obj;

            if (obj.GetType() == targetType)
                return obj;

            if (targetType == typeof(OrganizationRequest) && obj is OrganizationRequest req)
            {
                var type = GetRequestType(req.RequestName);
                if (type == null)
                    return obj;

                return new OrganizationRequest
                {
                    RequestName = req.RequestName,
                    Parameters = req.Parameters,
                    RequestId = req.RequestId
                };
            }
            else if (targetType == typeof(OrganizationResponse) && obj is OrganizationResponse resp)
            {
                var type = GetResponseType(resp.ResponseName);
                if (type == null)
                    return obj;

                return new OrganizationResponse
                {
                    ResponseName = resp.ResponseName,
                    Results = resp.Results
                };
            }
            else if (obj is Entity entity && obj.GetType() != typeof(Entity))
            {
                return entity.ToEntity<Entity>();
            }

            return obj;
        }

#if NETFRAMEWORK
        object IDataContractSurrogate.GetCustomDataToExport(MemberInfo memberInfo, Type dataContractType)
        {
            return null;
        }

        object IDataContractSurrogate.GetCustomDataToExport(Type clrType, Type dataContractType)
        {
            return null;
        }

        void IDataContractSurrogate.GetKnownCustomDataTypes(Collection<Type> customDataTypes)
        {
        }

        Type IDataContractSurrogate.GetReferencedTypeOnImport(string typeName, string typeNamespace, object customData)
        {
            return null;
        }

        CodeTypeDeclaration IDataContractSurrogate.ProcessImportedType(CodeTypeDeclaration typeDeclaration, CodeCompileUnit compileUnit)
        {
            return null;
        }
#endif

        private Type GetRequestType(string name)
        {
            if (_requestTypes.TryGetValue(name, out var requestType))
                return requestType;

            return null;
        }

        private Type GetResponseType(string name)
        {
            if (_responseTypes.TryGetValue(name, out var responseType))
                return responseType;

            return null;
        }

        private Type GetEntityType(string name)
        {
            if (_entityTypes.TryGetValue(name, out var entityType))
                return entityType;

            return null;
        }
    }
}
