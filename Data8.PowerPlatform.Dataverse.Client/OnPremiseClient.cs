using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Federation;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Data8.PowerPlatform.Dataverse.Client
{
    /// <summary>
    /// Implements <see cref="IOrganizationService"/> using SOAP authenticated via IFD using WS-Trust username and password
    /// </summary>
    public class OnPremiseClient : IOrganizationService
    {
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
            var policies = Wsdl.WsdlLoader.Load(url + "?wsdl&sdkversion=" + _sdkMajorVersion)
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
                    throw new NotSupportedException("Active Directory authentication is not supported. Please enable Claims-Based Authentication");

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
            var client = new WSTrustClient(url, usernameWsTrust13Port.Address.Location);
            client.ChannelFactory.Credentials.UserName.UserName = credentials.UserName.UserName;
            client.ChannelFactory.Credentials.UserName.Password = credentials.UserName.Password;
            return client.ChannelFactory.CreateChannel();
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
            get => ((IContextChannel)_service).OperationTimeout;
            set => ((IContextChannel)_service).OperationTimeout = value;
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

        /// <summary>
        /// Inner client to set up the SOAP channel using WS-Trust
        /// </summary>
        class WSTrustClient : ClientBase<IOrganizationService>
        {
            /// <summary>
            /// Creates a new <see cref="WSTrustClient"/>
            /// </summary>
            /// <param name="url">The URL of the organization service</param>
            /// <param name="issuerEndpoint">The URL of the STS endpoint</param>
            public WSTrustClient(string url, string issuerEndpoint) : base(CreateBinding(issuerEndpoint), new EndpointAddress(url))
            {
            }

            private static Binding CreateBinding(string issuerEndpoint)
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

                // This workaround is necessary only until an updated System.ServiceModel.Federation ships with
                // https://github.com/dotnet/wcf/issues/4426 fixed.
                // The CreateWSFederationTokenParameters helper method does not have this bug.
                tokenParameters.KeyType = SecurityKeyType.SymmetricKey;

                // Create the WSFederationHttpBinding
                var binding = new WSFederationHttpBinding(tokenParameters);

                // Turn off security context - MSCRM doesn't understand it
                binding.Security.Message.EstablishSecurityContext = false;

                // Increase maximum allowed sizes to allow receiving large messages
                binding.MaxReceivedMessageSize = Int32.MaxValue;
                binding.MaxBufferPoolSize = Int32.MaxValue;
                binding.ReaderQuotas.MaxStringContentLength = Int32.MaxValue;
                binding.ReaderQuotas.MaxArrayLength = Int32.MaxValue;
                binding.ReaderQuotas.MaxBytesPerRead = Int32.MaxValue;
                binding.ReaderQuotas.MaxNameTableCharCount = Int32.MaxValue;

                return binding;
            }
        }

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
                o.KeyEntropyMode = System.ServiceModel.Security.SecurityKeyEntropyMode.ServerEntropy;
                return o;
            }
        }

        /// <summary>
        /// Adds headers into the SOAP requests
        /// </summary>
        class OrgServiceScope : IDisposable
        {
            private readonly OperationContextScope _scope;

            public OrgServiceScope(IOrganizationService svc, Guid callerId)
            {
                _scope = new OperationContextScope((IContextChannel) svc);

                OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader("SdkClientVersion", Wsdl.Namespaces.tns, _sdkVersion));
                OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader("UserType", Wsdl.Namespaces.tns, "CrmUser"));

                if (callerId != Guid.Empty)
                    OperationContext.Current.OutgoingMessageHeaders.Add(MessageHeader.CreateHeader("CallerId", Wsdl.Namespaces.tns, callerId));
            }

            public void Dispose()
            {
                _scope.Dispose();
            }
        }
    }
}
