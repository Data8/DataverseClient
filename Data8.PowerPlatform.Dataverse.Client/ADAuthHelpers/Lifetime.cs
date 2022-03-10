using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    class Lifetime
    {
        public Lifetime() : this(DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5))
        {
        }

        public Lifetime(DateTime created, DateTime expires)
        {
            Created = created;
            Expires = expires;
        }

        public DateTime Created { get; private set; }

        public DateTime Expires { get; private set; }

        public static Lifetime Read(XmlDictionaryReader reader)
        {
            reader.ReadStartElement(nameof(Created), Namespaces.WSSecurityUtility);
            var created = reader.ReadContentAsDateTime();
            reader.ReadEndElement(); // u:Created

            reader.ReadStartElement(nameof(Expires), Namespaces.WSSecurityUtility);
            var expires = reader.ReadContentAsDateTime();
            reader.ReadEndElement(); // u:Expires

            return new Lifetime(created, expires);
        }
    }
}
