using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    class SecurityContextToken
    {
        private SecurityContextToken()
        {
        }

        public string Id { get; private set; }

        public string Identifier { get; private set; }

        public static SecurityContextToken Read(XmlDictionaryReader reader)
        {
            var id = reader.GetAttribute(nameof(Id), Namespaces.WSSecurityUtility);
            reader.ReadStartElement(nameof(SecurityContextToken), Namespaces.WSSecureConversation);

            reader.ReadStartElement(nameof(Identifier), Namespaces.WSSecureConversation);
            var identifier = reader.ReadString();
            reader.ReadEndElement(); // c:Identifier

            reader.ReadEndElement(); // c:SecurityContextToken

            return new SecurityContextToken
            {
                Id = id,
                Identifier = identifier
            };
        }
    }
}
