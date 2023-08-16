using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using Data8.PowerPlatform.Dataverse.Client.Wsdl;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;

namespace Data8.PowerPlatform.Dataverse.Client
{
    /// <summary>
    /// Implements <see cref="IOrganizationService"/> using SOAP authenticated via WS-Trust username and password.
    /// </summary>
    /// <remarks>
    /// Claims-based authentication, IFD and Active Directory authentication are all supported.
    /// </remarks>
    public class OnPremiseClient : IOrganizationServiceAsync2
    {
        /// <summary>
        /// Adds headers into the SOAP requests
        /// </summary>
        class OrgServiceScope : IDisposable
        {
            private readonly OperationContextScope _scope;

            public OrgServiceScope(IInnerOrganizationService svc, Guid callerId)
            {
                if (svc is ClaimsBasedAuthClient cbac)
                {
                    _scope = new OperationContextScope(cbac.InnerChannel);

                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader("SdkClientVersion", Wsdl.Namespaces.tns, _sdkVersion));
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader("UserType", Wsdl.Namespaces.tns, "CrmUser"));

                    if (callerId != Guid.Empty)
                        OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader("CallerId", Wsdl.Namespaces.tns, callerId));
                }
                else
                {
                    ((ADAuthClient)svc).SdkClientVersion = _sdkVersion;
                    ((ADAuthClient)svc).CallerId = callerId;
                }
            }

            public void Dispose()
            {
                _scope?.Dispose();
            }
        }

        private readonly IInnerOrganizationService _innerService;
        private readonly IOrganizationServiceAsync _service;

        private static readonly string _sdkVersion;
        private static readonly int _sdkMajorVersion;

        static OnPremiseClient()
        {
            // Get the version number of the SDK we're using
            var assembly = typeof(IOrganizationService).Assembly;

            if (!string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
            {
                var ver = FileVersionInfo.GetVersionInfo(assembly.Location);
                _sdkVersion = ver.FileVersion;
                _sdkMajorVersion = ver.FileMajorPart;
            }
            else
            {
                _sdkVersion = "9.1.2.3";
                _sdkMajorVersion = 9;
            }
        }

        /// <summary>
        /// Creates a new <see cref="OnPremiseClient"/> using default credentials
        /// </summary>
        /// <param name="url">The URL of the organization service to connect to</param>
        /// <remarks>
        /// The <paramref name="url"/> must include the full path to the organization service, e.g. https://org.crm.contoso.com/XRMServices/2011/Organization.svc
        /// </remarks>
        public OnPremiseClient(string url)
            : this(url, new ClientCredentials())
        {
        }

        /// <summary>
        /// Creates a new <see cref="OnPremiseClient"/>
        /// </summary>
        /// <param name="url">The URL of the organization service to connect to</param>
        /// <param name="username">The username to authenticate as</param>
        /// <param name="password">The password to authenticate with</param>
        /// <remarks>
        /// The <paramref name="url"/> must include the full path to the organization service, e.g. https://org.crm.contoso.com/XRMServices/2011/Organization.svc
        /// </remarks>
        public OnPremiseClient(string url, string username, string password) : this(url, new ClientCredentials { UserName = { UserName = username, Password = password } })
        {
        }

        /// <summary>
        /// Creates a new <see cref="OnPremiseClient"/>
        /// </summary>
        /// <param name="url">The URL of the organization service to connect to</param>
        /// <param name="credentials">The credentials to use to authenticate with</param>
        /// <remarks>
        /// The <paramref name="url"/> must include the full path to the organization service, e.g. https://org.crm.contoso.com/XRMServices/2011/Organization.svc
        /// </remarks>
        public OnPremiseClient(string url, ClientCredentials credentials)
        {
            if (!new Uri(url).Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Only https connections are supported");

            // Get the WSDL of the target to find the authentication type and the URL of the STS for Federated auth
            var wsdl = Wsdl.WsdlLoader.Load(url + "?wsdl&sdkversion=" + _sdkMajorVersion).ToList();

            var policies = wsdl
                .Where(w => w.Policies != null)
                .SelectMany(w => w.Policies)
                .ToList();

            var authenticationPolicy = policies
                .Select(p => p.FindPolicyItem<AuthenticationPolicy>())
                .FirstOrDefault(t => t != null);

            if (authenticationPolicy == null)
                throw new InvalidOperationException("Unable to find authentication policy");

            switch (authenticationPolicy.Authentication)
            {
                case Wsdl.AuthenticationType.ActiveDirectory:
                    var identity = wsdl
                        .Where(w => w.Services != null)
                        .SelectMany(w => w.Services)
                        .Single()
                        .Ports
                        .Single(port => new Uri(port.Address.Location).Scheme.Equals(new Uri(url).Scheme, StringComparison.OrdinalIgnoreCase))
                        .EndpointReference
                        .Identity;

                    _innerService = ConnectAD(url, credentials, identity?.Upn ?? identity?.Spn);
                    break;

                case Wsdl.AuthenticationType.Federation:
                    _innerService = ConnectFederated(url, credentials, policies);
                    break;

                default:
                    throw new NotSupportedException("Unknown authentication policy " + authenticationPolicy.Authentication);
            }

            if (_innerService is IOrganizationServiceAsync async)
                _service = async;
            else
                _service = new OrgServiceAsyncWrapper(_innerService);

            Timeout = TimeSpan.FromMinutes(2);
        }

        private ClaimsBasedAuthClient ConnectFederated(string url, ClientCredentials credentials, List<Policy> policies)
        {
            var tokenEndpoint = policies
                .Select(p => p.FindPolicyItem<EndorsingSupportingTokens>())
                .FirstOrDefault(t => t != null);

            var issuer = tokenEndpoint.Policy.FindPolicyItem<Wsdl.IssuedToken>();
            var issuerMetadataEndpoint = issuer.Issuer.Metadata.Metadata.MetadataSection.MetadataReference.Address;

            // Now get the WSDL of the STS to get the username and password endpoint
            var issuerWsdls = WsdlLoader.Load(issuerMetadataEndpoint).ToList();
            var issuerPolicies = issuerWsdls
                .Where(wsdl => wsdl.Policies != null)
                .SelectMany(wsdl => wsdl.Policies)
                .ToList();

            var usernameWsTrust13Policy = issuerPolicies
                .FirstOrDefault(p => p.FindPolicyItem<SignedEncryptedSupportingTokens>()?.Policy.FindPolicyItem<UsernameToken>() != null && p.FindPolicyItem<Trust13>() != null);

            var issuerBindings = issuerWsdls
                .Where(wsdl => wsdl.Bindings != null)
                .SelectMany(wsdl => wsdl.Bindings)
                .ToList();

            var usernameWsTrust13Binding = issuerBindings
                .FirstOrDefault(b => b.PolicyReference.Uri == "#" + usernameWsTrust13Policy.Id);

            var issuerPorts = issuerWsdls
                .Where(wsdl => wsdl.Services != null)
                .SelectMany(wsdl => wsdl.Services)
                .SelectMany(svc => svc.Ports)
                .ToList();

            var usernameWsTrust13Port = issuerPorts
                .FirstOrDefault(p => p.Binding == "tns:" + usernameWsTrust13Binding.Name);

            // Create the SOAP client to authenticate against the STS
            var client = new ClaimsBasedAuthClient(url, usernameWsTrust13Port.Address.Location);
            client.ChannelFactory.Credentials.UserName.UserName = credentials.UserName.UserName;
            client.ChannelFactory.Credentials.UserName.Password = credentials.UserName.Password;
            return client;
        }

        private ADAuthClient ConnectAD(string url, ClientCredentials credentials, string identity)
        {
            var client = new ADAuthClient(url, credentials.UserName.UserName, credentials.UserName.Password, identity);
            return client;
        }

        /// <inheritdoc cref="ServiceClient.CallerId"/>
        public Guid CallerId { get; set; }

        /// <inheritdoc cref="ServiceClient.MaxConnectionTimeout"/>
        public TimeSpan Timeout
        {
            get
            {
                return _innerService.Timeout;
            }
            set
            {
                _innerService.Timeout = value;
            }
        }

        /// <summary>
        /// Enables support for the early-bound entity types.
        /// </summary>
        /// <remarks>
        /// Early bound types will be loaded from an already-loaded assembly that is marked with the <see cref="ProxyTypesAssemblyAttribute"/>
        /// </remarks>
        public void EnableProxyTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetCustomAttribute<ProxyTypesAssemblyAttribute>() != null)
                {
                    EnableProxyTypes(assembly);
                    break;
                }
            }
        }

        /// <summary>
        /// Enables support for the early-bound entity types exposed in a specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly to load the early-bound types from</param>
        public void EnableProxyTypes(Assembly assembly)
        {
            _innerService.EnableProxyTypes(assembly);
        }

        private IDisposable StartScope()
        {
            return new OrgServiceScope(_innerService, CallerId);
        }

        /// <inheritdoc/>
        public virtual void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            using (StartScope())
            {
                _service.Associate(entityName, entityId, relationship, relatedEntities);
            }
        }

        /// <inheritdoc/>
        public virtual Guid Create(Entity entity)
        {
            using (StartScope())
            {
                return _service.Create(entity);
            }
        }

        /// <inheritdoc/>
        public virtual void Delete(string entityName, Guid id)
        {
            using (StartScope())
            {
                _service.Delete(entityName, id);
            }
        }

        /// <inheritdoc/>
        public virtual void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            using (StartScope())
            {
                _service.Disassociate(entityName, entityId, relationship, relatedEntities);
            }
        }

        /// <inheritdoc/>
        public virtual OrganizationResponse Execute(OrganizationRequest request)
        {
            using (StartScope())
            {
                return _service.Execute(request);
            }
        }

        /// <inheritdoc/>
        public virtual Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            using (StartScope())
            {
                return _service.Retrieve(entityName, id, columnSet);
            }
        }

        /// <inheritdoc/>
        public virtual EntityCollection RetrieveMultiple(QueryBase query)
        {
            using (StartScope())
            {
                return _service.RetrieveMultiple(query);
            }
        }

        /// <inheritdoc/>
        public virtual void Update(Entity entity)
        {
            using (StartScope())
            {
                _service.Update(entity);
            }
        }

        /// <inheritdoc/>
        public async Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AssociateAsync(entityName, entityId, relationship, relatedEntities);
        }

        /// <inheritdoc/>
        public Task<Guid> CreateAsync(Entity entity, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CreateAsync(entity);
        }

        /// <inheritdoc/>
        public Task<Entity> CreateAndReturnAsync(Entity entity, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string entityName, Guid id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DeleteAsync(entityName, id);
        }

        /// <inheritdoc/>
        public async Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DisassociateAsync(entityName, entityId, relationship, relatedEntities);
        }

        /// <inheritdoc/>
        public async Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await ExecuteAsync(request);
        }

        /// <inheritdoc/>
        public async Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await RetrieveAsync(entityName, id, columnSet);
        }

        /// <inheritdoc/>
        public async Task<EntityCollection> RetrieveMultipleAsync(QueryBase query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await RetrieveMultipleAsync(query);
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(Entity entity, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateAsync(entity);
        }

        /// <inheritdoc/>
        public async Task<Guid> CreateAsync(Entity entity)
        {
            using (StartScope())
            {
                return await _service.CreateAsync(entity);
            }
        }

        /// <inheritdoc/>
        public async Task<Entity> RetrieveAsync(string entityName, Guid id, ColumnSet columnSet)
        {
            using (StartScope())
            {
                return await _service.RetrieveAsync(entityName, id, columnSet);
            }
        }

        /// <inheritdoc/>
        public async Task UpdateAsync(Entity entity)
        {
            using (StartScope())
            {
                await _service.UpdateAsync(entity);
            }
        }

        /// <inheritdoc/>
        public async Task DeleteAsync(string entityName, Guid id)
        {
            using (StartScope())
            {
                await _service.DeleteAsync(entityName, id);
            }
        }

        /// <inheritdoc/>
        public async Task<OrganizationResponse> ExecuteAsync(OrganizationRequest request)
        {
            using (StartScope())
            {
                return await _service.ExecuteAsync(request);
            }
        }

        /// <inheritdoc/>
        public async Task AssociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            using (StartScope())
            {
                await _service.AssociateAsync(entityName, entityId, relationship, relatedEntities);
            }
        }

        /// <inheritdoc/>
        public async Task DisassociateAsync(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            using (StartScope())
            {
                await _service.DisassociateAsync(entityName, entityId, relationship, relatedEntities);
            }
        }

        /// <inheritdoc/>
        public async Task<EntityCollection> RetrieveMultipleAsync(QueryBase query)
        {
            using (StartScope())
            {
                return await _service.RetrieveMultipleAsync(query);
            }
        }
    }
}