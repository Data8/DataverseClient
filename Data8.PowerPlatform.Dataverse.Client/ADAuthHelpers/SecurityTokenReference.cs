using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    class SecurityTokenReference
    {
        private SecurityTokenReference()
        {
        }

        public string ValueType { get; private set; }

        public string URI { get; private set; }

        public static SecurityTokenReference Read(XmlDictionaryReader reader)
        {
            reader.ReadStartElement(nameof(SecurityTokenReference), Namespaces.WSSecurityExtensions);

            var valueType = reader.GetAttribute(nameof(ValueType));
            var uri = reader.GetAttribute(nameof(URI));
            reader.ReadStartElement("Reference", Namespaces.WSSecurityExtensions);
            reader.ReadEndElement(); // o:Reference

            reader.ReadEndElement(); // o:SecurityTokenReference

            return new SecurityTokenReference
            {
                ValueType = valueType,
                URI = uri
            };
        }
    }
}
