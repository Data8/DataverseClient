using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    class BinaryExchange : BodyWriter
    {
        private const string SPNEGO = "http://schemas.xmlsoap.org/ws/2005/02/trust/spnego";
        private const string BASE64 = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary";

        public BinaryExchange(byte[] token) : base(isBuffered: true)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token));

            Token = token;
        }

        public byte[] Token { get; private set; }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("t", nameof(BinaryExchange), Namespaces.WSTrust);
            writer.WriteAttributeString("ValueType", SPNEGO);
            writer.WriteAttributeString("EncodingType", BASE64);

            writer.WriteString(Convert.ToBase64String(Token));

            writer.WriteEndElement(); // t:BinaryExchange
        }

        public static BinaryExchange Read(XmlDictionaryReader reader)
        {
            var valueType = reader.GetAttribute("ValueType");
            var encodingType = reader.GetAttribute("EncodingType");

            if (valueType != SPNEGO || encodingType != BASE64)
                throw new NotSupportedException();

            reader.ReadStartElement();
            var token = reader.ReadString();
            reader.ReadEndElement();

            return new BinaryExchange(Convert.FromBase64String(token));
        }
    }
}
