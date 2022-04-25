using System;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    class RequestSecurityToken : BaseAuthRequest
    {
        private readonly string _context;

        public RequestSecurityToken(byte[] token)
        {
            _context = "uuid-" + Guid.NewGuid().ToString();
            Token = new BinaryExchange(token);
        }

        protected override string Action => "http://schemas.xmlsoap.org/ws/2005/02/trust/RST/Issue";

        public BinaryExchange Token { get; private set; }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("t", "RequestSecurityToken", Namespaces.WSTrust);
            writer.WriteAttributeString("Context", _context);

            writer.WriteStartElement("t", "TokenType", Namespaces.WSTrust);
            writer.WriteString("http://schemas.xmlsoap.org/ws/2005/02/sc/sct");
            writer.WriteEndElement(); // t:TokenType

            writer.WriteStartElement("t", "RequestType", Namespaces.WSTrust);
            writer.WriteString("http://schemas.xmlsoap.org/ws/2005/02/trust/Issue");
            writer.WriteEndElement(); // t:RequestType

            writer.WriteStartElement("t", "KeySize", Namespaces.WSTrust);
            writer.WriteString("256");
            writer.WriteEndElement(); // t:RequestType

            Token.WriteBodyContents(writer);

            writer.WriteEndElement(); // t:RequestSecurityToken
        }
    }
}
