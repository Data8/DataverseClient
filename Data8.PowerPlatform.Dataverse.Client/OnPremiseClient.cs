using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Data8.PowerPlatform.Dataverse.Client
{
    /// <summary>
    /// Implements <see cref="IOrganizationService"/> using SOAP authenticated via WS-Trust username and password.
    /// </summary>
    /// <remarks>
    /// Claims-based authentication, IFD and Active Directory authentication are all supported.
    /// </remarks>
    public class OnPremiseClient : IOrganizationService
    {
        /// <summary>
        /// Adds headers into the SOAP requests
        /// </summary>
        class OrgServiceScope : IDisposable
        {
            private readonly OperationContextScope _scope;

            public OrgServiceScope(IOrganizationService svc, Guid callerId)
            {
                if (svc is IContextChannel channel)
                {
                    _scope = new OperationContextScope((IContextChannel)svc);

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

        private readonly IOrganizationService _service;

        private static readonly string _sdkVersion;
        private static readonly int _sdkMajorVersion;

        static OnPremiseClient()
        {
            // Get the version number of the SDK we're using
            var assembly = typeof(IOrganizationService).Assembly;

            if (!String.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
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
            // Get the WSDL of the target to find the authentication type and the URL of the STS for Federated auth
            var wsdl = Wsdl.WsdlLoader.Load(url + "?wsdl&sdkversion=" + _sdkMajorVersion).ToList();

            var policies = wsdl
                .Where(wsdl => wsdl.Policies != null)
                .SelectMany(wsdl => wsdl.Policies)
                .ToList();

            var authenticationPolicy = policies
                .Select(p => p.FindPolicyItem<Wsdl.AuthenticationPolicy>())
                .Where(t => t != null)
                .FirstOrDefault();

            if (authenticationPolicy == null)
                throw new InvalidOperationException("Unable to find authentication policy");

            switch (authenticationPolicy.Authentication)
            {
                case Wsdl.AuthenticationType.ActiveDirectory:
                    var identity = wsdl
                        .Where(wsdl => wsdl.Services != null)
                        .SelectMany(wsdl => wsdl.Services)
                        .Single()
                        .Ports.Last()
                        .EndpointReference
                        .Identity
                        .Upn;

                    _service = ConnectAD(url, credentials, identity);
                    break;

                case Wsdl.AuthenticationType.Federation:
                    _service = ConnectFederated(url, credentials, policies);
                    break;

                default:
                    throw new NotSupportedException("Unknown authentication policy " + authenticationPolicy.Authentication);
            }

            Timeout = TimeSpan.FromMinutes(2);
        }

        private IOrganizationService ConnectFederated(string url, ClientCredentials credentials, List<Wsdl.Policy> policies)
        {
            var tokenEndpoint = policies
                .Select(p => p.FindPolicyItem<Wsdl.EndorsingSupportingTokens>())
                .Where(t => t != null)
                .FirstOrDefault();

            var issuer = tokenEndpoint.Policy.FindPolicyItem<Wsdl.IssuedToken>();
            var issuerMetadataEndpoint = issuer.Issuer.Metadata.Metadata.MetadataSection.MetadataReference.Address;

            // Now get the WSDL of the STS to get the username and password endpoint
            var issuerWsdls = Wsdl.WsdlLoader.Load(issuerMetadataEndpoint).ToList();
            var issuerPolicies = issuerWsdls
                .Where(wsdl => wsdl.Policies != null)
                .SelectMany(wsdl => wsdl.Policies)
                .ToList();

            var usernameWsTrust13Policy = issuerPolicies
                .Where(p => p.FindPolicyItem<Wsdl.SignedEncryptedSupportingTokens>()?.Policy.FindPolicyItem<Wsdl.UsernameToken>() != null && p.FindPolicyItem<Wsdl.Trust13>() != null)
                .FirstOrDefault();

            var issuerBindings = issuerWsdls
                .Where(wsdl => wsdl.Bindings != null)
                .SelectMany(wsdl => wsdl.Bindings)
                .ToList();

            var usernameWsTrust13Binding = issuerBindings
                .Where(b => b.PolicyReference.Uri == "#" + usernameWsTrust13Policy.Id)
                .FirstOrDefault();

            var issuerPorts = issuerWsdls
                .Where(wsdl => wsdl.Services != null)
                .SelectMany(wsdl => wsdl.Services)
                .SelectMany(svc => svc.Ports)
                .ToList();

            var usernameWsTrust13Port = issuerPorts
                .Where(p => p.Binding == "tns:" + usernameWsTrust13Binding.Name)
                .FirstOrDefault();

            // Create the SOAP client to authenticate against the STS
            var client = new ClaimsBasedAuthClient(url, usernameWsTrust13Port.Address.Location);
            client.ChannelFactory.Credentials.UserName.UserName = credentials.UserName.UserName;
            client.ChannelFactory.Credentials.UserName.Password = credentials.UserName.Password;
            return client.ChannelFactory.CreateChannel();
        }

        private IOrganizationService ConnectAD(string url, ClientCredentials credentials, string identity)
        {
            var client = new ADAuthClient(url, credentials.UserName.UserName, credentials.UserName.Password, identity);
            return client;
        }

        /// <summary>
        /// Returns or sets the ID of the user that should be impersonated
        /// </summary>
        /// <remarks>
        /// Use <see cref="Guid.Empty"/> to disable impersonation
        /// </remarks>
        public Guid CallerId { get; set; }

        /// <summary>
        /// Sets the timeout for each operation
        /// </summary>
        public TimeSpan Timeout
        {
            get
            {
                if (_service is IContextChannel channel)
                    return channel.OperationTimeout;
                else
                    return ((ADAuthClient)_service).Timeout;
            }
            set
            {
                if (_service is IContextChannel channel)
                    channel.OperationTimeout = value;
                else
                    ((ADAuthClient)_service).Timeout = value;
            }
        }

        private IDisposable StartScope()
        {
            return new OrgServiceScope(_service, CallerId);
        }

        /// <inheritdoc/>
        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            using (StartScope())
            {
                _service.Associate(entityName, entityId, relationship, relatedEntities);
            }
        }

        /// <inheritdoc/>
        public Guid Create(Entity entity)
        {
            using (StartScope())
            {
                return _service.Create(entity);
            }
        }

        /// <inheritdoc/>
        public void Delete(string entityName, Guid id)
        {
            using (StartScope())
            {
                _service.Delete(entityName, id);
            }
        }

        /// <inheritdoc/>
        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            using (StartScope())
            {
                _service.Disassociate(entityName, entityId, relationship, relatedEntities);
            }
        }

        /// <inheritdoc/>
        public OrganizationResponse Execute(OrganizationRequest request)
        {
            using (StartScope())
            {
                return _service.Execute(request);
            }
        }

        /// <inheritdoc/>
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            using (StartScope())
            {
                return _service.Retrieve(entityName, id, columnSet);
            }
        }

        /// <inheritdoc/>
        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            using (StartScope())
            {
                return _service.RetrieveMultiple(query);
            }
        }

        /// <inheritdoc/>
        public void Update(Entity entity)
        {
            using (StartScope())
            {
                _service.Update(entity);
            }
        }
    }
}
