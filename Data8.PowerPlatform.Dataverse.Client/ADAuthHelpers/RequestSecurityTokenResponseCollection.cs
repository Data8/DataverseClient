using System.Collections.Generic;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    class RequestSecurityTokenResponseCollection
    {
        private RequestSecurityTokenResponseCollection(List<RequestSecurityTokenResponse> rstrs)
        {
            Responses = rstrs.AsReadOnly();
        }

        public IReadOnlyList<RequestSecurityTokenResponse> Responses { get; private set; }

        public static RequestSecurityTokenResponseCollection Read(XmlDictionaryReader reader, Authenticator auth)
        {
            // Add the *first* response to the hash, except for the RequestedSecurityToken and RequestedProofToken elements

            var rstrs = new List<RequestSecurityTokenResponse>();

            reader.ReadStartElement(nameof(RequestSecurityTokenResponseCollection), Namespaces.WSTrust);

            while (reader.NodeType == XmlNodeType.Element)
                rstrs.Add(RequestSecurityTokenResponse.Read(reader, rstrs.Count == 0 ? auth : null, true));

            reader.ReadEndElement(); // t:RequestSecurityTokenResponseCollection

            return new RequestSecurityTokenResponseCollection(rstrs);
        }
    }
}
