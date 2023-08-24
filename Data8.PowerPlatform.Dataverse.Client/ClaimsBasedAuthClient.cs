// Microsoft.Xrm.Sdk now includes various WCF-related classes with the same namespaces & names as in the
// System.ServiceModel.Security package. That package is now referenced with the alias SSS so we can specify
// which version we want to use.
extern alias SSS;

using System;
using System.ServiceModel;
using System.ServiceModel.Federation;
using System.Reflection;
using System.ServiceModel.Description;
using Binding = System.ServiceModel.Channels.Binding;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Query;

#if NET462_OR_GREATER
using WSFederationHttpBinding = System.ServiceModel.Federation.WSFederationHttpBinding;

using SecurityBindingElement = System.ServiceModel.Channels.SecurityBindingElement;
using SecurityKeyEntropyMode = System.ServiceModel.Security.SecurityKeyEntropyMode;

#else
using SecurityBindingElement = SSS.System.ServiceModel.Channels.SecurityBindingElement;
using SecurityKeyEntropyMode = SSS.System.ServiceModel.Security.SecurityKeyEntropyMode;
#endif

namespace Data8.PowerPlatform.Dataverse.Client
{
    /// <summary>
    /// Inner client to set up the SOAP channel using WS-Trust
    /// </summary>
#if NETCOREAPP
    class ClaimsBasedAuthClient : ClientBase<IOrganizationServiceAsync>, IOrganizationServiceAsync, IInnerOrganizationService
#else
    class ClaimsBasedAuthClient : ClientBase<IOrganizationService>, IOrganizationService, IInnerOrganizationService
#endif
    {
        private readonly ProxySerializationSurrogate _serializationSurrogate;

        /// <summary>
        /// A binding for WS-Trust that uses server entropy
        /// </summary>
        class ServerEntropyWS2007HttpBinding : WS2007HttpBinding
        {
            public ServerEntropyWS2007HttpBinding(SecurityMode securityMode) : base(securityMode)
            {
            }

            protected override SecurityBindingElement CreateMessageSecurity()
            {
                // Use server entropy to match SDK
                var o = base.CreateMessageSecurity();
                o.KeyEntropyMode = SecurityKeyEntropyMode.ServerEntropy;
                return o;
            }
        }

        /// <summary>
        /// Creates a new <see cref="ClaimsBasedAuthClient"/>
        /// </summary>
        /// <param name="url">The URL of the organization service</param>
        /// <param name="issuerEndpoint">The URL of the STS endpoint</param>
        public ClaimsBasedAuthClient(string url, string issuerEndpoint) : base(CreateServiceEndpoint(url, issuerEndpoint))
        {
            _serializationSurrogate = new ProxySerializationSurrogate();

            foreach (var operation in Endpoint.Contract.Operations)
            {
                var operationBehavior = operation.Behaviors.Find<DataContractSerializerOperationBehavior>();
#if NETCOREAPP
                operationBehavior.SerializationSurrogateProvider = _serializationSurrogate;
#else
                operationBehavior.DataContractSurrogate = _serializationSurrogate;
#endif
            }
        }

        private static ServiceEndpoint CreateServiceEndpoint(string url, string issuerEndpoint)
        {
            var binding = CreateFederatedBinding(issuerEndpoint);
            var endpointAddress = new EndpointAddress(url);

            var serviceInterfaceType = typeof(ClaimsBasedAuthClient).BaseType.GetGenericArguments()[0];
            var serviceEndpoint = new ServiceEndpoint(ContractDescription.GetContract(serviceInterfaceType), binding, endpointAddress);

            foreach (var operation in serviceEndpoint.Contract.Operations)
            {
                var operationBehavior = operation.Behaviors.Find<DataContractSerializerOperationBehavior>();
                operationBehavior.MaxItemsInObjectGraph = int.MaxValue;
            }

            return serviceEndpoint;
        }

        public TimeSpan Timeout
        {
            get { return InnerChannel.OperationTimeout; }
            set { InnerChannel.OperationTimeout = value; }
        }

        public void EnableProxyTypes(Assembly assembly)
        {
            _serializationSurrogate.LoadAssembly(assembly);
        }

        private static Binding CreateFederatedBinding(string issuerEndpoint)
        {
            // Ref: https://devblogs.microsoft.com/dotnet/wsfederationhttpbinding-in-net-standard-wcf/

            // First, create the inner binding for communicating with the token issuer.
            // The security settings will be specific to the STS and should mirror what
            // would have been in an app.config in a .NET Framework scenario.
            var issuerBinding = new ServerEntropyWS2007HttpBinding(SecurityMode.TransportWithMessageCredential);
            issuerBinding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            issuerBinding.Security.Message.EstablishSecurityContext = false;

            // Next, create the token issuer's endpoint address
            var endpointAddress = new EndpointAddress(issuerEndpoint);

            // Finally, create the WSTrustTokenParameters
            var tokenParameters = WSTrustTokenParameters.CreateWS2007FederationTokenParameters(issuerBinding, endpointAddress);

            // Create the WSFederationHttpBinding
            var binding = new WSFederationHttpBinding(tokenParameters);

            // Turn off security context - MSCRM doesn't understand it
            binding.Security.Message.EstablishSecurityContext = false;

            // Increase maximum allowed sizes to allow receiving large messages
            binding.MaxReceivedMessageSize = int.MaxValue;
            binding.MaxBufferPoolSize = int.MaxValue;
            binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
            binding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            binding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
            binding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue;

            return binding;
        }

#if NETCOREAPP
        public Task<Guid> CreateAsync(Entity entity)
        {
            return Channel.CreateAsync(entity);
        }

        public Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet)
        {
            return Channel.RetrieveAsync(entityName, id, columnSet);
        }

        public Task UpdateAsync(Entity entity)
        {
            return Channel.UpdateAsync(entity);
        }

        public Task DeleteAsync(string entityName, Guid id)
        {
            return Channel.DeleteAsync(entityName, id);
        }

        public Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request)
        {
            return Channel.ExecuteAsync(request);
        }

        public Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            return Channel.AssociateAsync(entityName, entityId, relationship, relatedEntities);
        }

        public Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            return Channel.DisassociateAsync(entityName, entityId, relationship, relatedEntities);
        }

        public Task<EntityCollection> RetrieveMultipleAsync(QueryBase query)
        {
            return Channel.RetrieveMultipleAsync(query);
        }
#endif

        public Guid Create(Entity entity)
        {
            return Channel.Create(entity);
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            return Channel.Retrieve(entityName, id, columnSet);
        }

        public void Update(Entity entity)
        {
            Channel.Update(entity);
        }

        public void Delete(string entityName, Guid id)
        {
            Channel.Delete(entityName, id);
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            return Channel.Execute(request);
        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            Channel.Associate(entityName, entityId, relationship, relatedEntities);
        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            Channel.Disassociate(entityName, entityId, relationship, relatedEntities);
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            return Channel.RetrieveMultiple(query);
        }
    }
}
