// Microsoft.Xrm.Sdk now includes various WCF-related classes with the same namespaces & names as in the
// System.ServiceModel.Security package. That package is now referenced with the alias SSS so we can specify
// which version we want to use.
extern alias SSS;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Federation;
using Microsoft.Xrm.Sdk;

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
    class ClaimsBasedAuthClient : ClientBase<IOrganizationService>
    {
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
        public ClaimsBasedAuthClient(string url, string issuerEndpoint) : base(CreateFederatedBinding(issuerEndpoint), new EndpointAddress(url))
        {
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
    }
}
