using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    class CombinedHash
    {
        private CombinedHash(byte[] token)
        {
            Token = token;
        }

        public byte[] Token { get; private set; }

        public static CombinedHash Read(XmlDictionaryReader reader)
        {
            reader.ReadStartElement(nameof(CombinedHash), Namespaces.WSTrust);
            var token = reader.ReadString();
            reader.ReadEndElement(); // t:CombinedHash

            return new CombinedHash(Convert.FromBase64String(token));
        }
    }
}
