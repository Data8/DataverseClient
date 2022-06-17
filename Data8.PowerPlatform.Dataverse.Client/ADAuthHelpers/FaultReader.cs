using Microsoft.Xrm.Sdk;
using System;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    static class FaultReader
    {
        public static Exception ReadFault(XmlDictionaryReader bodyReader, string action)
        {
            bodyReader.ReadStartElement("Fault", Namespaces.Soap);

            bodyReader.ReadStartElement("Code", Namespaces.Soap);
            bodyReader.ReadStartElement("Value", Namespaces.Soap);
            var faultCode = bodyReader.ReadString();
            var faultCodeParts = faultCode.Split(':');
            var faultCodeName = faultCodeParts[0];
            var faultCodeNS = "";
            if (faultCodeParts.Length > 1)
            {
                faultCodeNS = bodyReader.LookupNamespace(faultCodeParts[0]);
                faultCodeName = faultCodeParts[1];
            }
            bodyReader.ReadEndElement(); // Value

            FaultCode subCode = null;

            if (bodyReader.NodeType == XmlNodeType.Element && bodyReader.LocalName == "Subcode" && bodyReader.NamespaceURI == Namespaces.Soap)
            {
                bodyReader.ReadStartElement("Subcode", Namespaces.Soap);
                bodyReader.ReadStartElement("Value", Namespaces.Soap);
                var faultSubCode = bodyReader.ReadString();
                var faultSubCodeParts = faultSubCode.Split(':');
                var faultSubCodeName = faultSubCodeParts[0];
                var faultSubCodeNS = "";
                if (faultSubCodeParts.Length > 1)
                {
                    faultSubCodeNS = bodyReader.LookupNamespace(faultSubCodeParts[0]);
                    faultSubCodeName = faultSubCodeParts[1];
                }
                bodyReader.ReadEndElement(); // Value
                bodyReader.ReadEndElement(); // Subcode

                subCode = new FaultCode(faultSubCodeName, faultSubCodeNS);
            }

            bodyReader.ReadEndElement(); // Code

            bodyReader.ReadStartElement("Reason", Namespaces.Soap);
            bodyReader.ReadStartElement("Text", Namespaces.Soap);
            var reason = bodyReader.ReadString();
            bodyReader.ReadEndElement(); // Text
            bodyReader.ReadEndElement(); // Reason

            if (bodyReader.NodeType == XmlNodeType.Element && bodyReader.LocalName == "Detail" && bodyReader.NamespaceURI == Namespaces.Soap)
            {
                bodyReader.ReadStartElement("Detail", Namespaces.Soap);

                if (bodyReader.NodeType == XmlNodeType.Element && bodyReader.LocalName == "OrganizationServiceFault" && bodyReader.NamespaceURI == Namespaces.Xrm2011Contracts)
                {
                    var serializer = new DataContractSerializer(typeof(OrganizationServiceFault));
                    var detail = (OrganizationServiceFault)serializer.ReadObject(bodyReader);

                    return new FaultException<OrganizationServiceFault>(detail, new FaultReason(reason), new FaultCode(faultCodeName, faultCodeNS, subCode), action);
                }
                else
                {
                    bodyReader.ReadSubtree();
                }

                bodyReader.ReadEndElement(); // Detail
            }

            bodyReader.ReadEndElement(); // Fault

            return new FaultException(new FaultReason(reason), new FaultCode(faultCodeName, faultCodeNS, subCode), action);
        }
    }
}
