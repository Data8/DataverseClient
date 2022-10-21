using System;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    class EncryptedKey
    {
        private const string GSS_WRAP = "http://schemas.xmlsoap.org/2005/02/trust/spnego#GSS_Wrap";

        private EncryptedKey()
        {
        }

        public byte[] CipherValue { get; private set; }

        public static EncryptedKey Read(XmlDictionaryReader reader)
        {
            reader.ReadStartElement(nameof(EncryptedKey), Namespaces.XmlEncryption);

            var algorithm = reader.GetAttribute("Algorithm");
            if (algorithm != GSS_WRAP)
                throw new NotSupportedException();

            reader.ReadStartElement("EncryptionMethod", Namespaces.XmlEncryption);
            reader.ReadEndElement(); // e:EncryptionMethod

            reader.ReadStartElement("CipherData", Namespaces.XmlEncryption);
            reader.ReadStartElement(nameof(CipherValue), Namespaces.XmlEncryption);
            var token = reader.ReadString();
            reader.ReadEndElement(); // e:CipherValue
            reader.ReadEndElement(); // e:CipherData

            reader.ReadEndElement(); // e:EncryptedKey

            return new EncryptedKey
            {
                CipherValue = Convert.FromBase64String(token)
            };
        }
    }
}
