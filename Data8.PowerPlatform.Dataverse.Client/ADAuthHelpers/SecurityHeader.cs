using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    class SecurityHeader : MessageHeader
    {
        private readonly SecurityContextToken _securityContextToken;
        private readonly byte[] _proofToken;

        public SecurityHeader(SecurityContextToken securityContextToken, byte[] proofToken)
        {
            _securityContextToken = securityContextToken;
            _proofToken = proofToken;
        }

        public override string Name => "Security";

        public override string Namespace => Namespaces.WSSecurityExtensions;

        protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            writer.WriteStartElement("o", Name, Namespace);
            writer.WriteAttributeString("s", "mustUnderstand", Namespaces.Soap, "1");
        }

        public override bool MustUnderstand => true;

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            // Create the signature. D365 expects SHA1 hashes but SignedXml on .NET Core doesn't support it, so work it
            // out manually.

            // 1. Create the XML that will be signed:
            // <u:Timestamp u:Id="_0">
            //   <u:Created>yyyy-MM-ddTHH:mm:ss.fffZ</u:Created>
            //   <u:Expires>yyyy-MM-ddTHH:mm:ss.fffZ</u:Expires>
            // </u:Timestamp>
            var timestampDoc = new XmlDocument();
            timestampDoc.PreserveWhitespace = false;
            var timestamp = timestampDoc.CreateElement("u", "Timestamp", Namespaces.WSSecurityUtility);
            timestamp.SetAttribute("u:Id", "_0");
            timestampDoc.AppendChild(timestamp);
            var created = timestampDoc.CreateElement("u", "Created", Namespaces.WSSecurityUtility);
            timestamp.AppendChild(created);
            created.AppendChild(timestampDoc.CreateTextNode(DateTime.UtcNow.ToString("s") + ".000Z"));
            var expires = timestampDoc.CreateElement("u", "Expires", Namespaces.WSSecurityUtility);
            timestamp.AppendChild(expires);
            expires.AppendChild(timestampDoc.CreateTextNode(DateTime.UtcNow.AddMinutes(5).ToString("s") + ".000Z"));

            // 2. Copy the to-be-signed XML to the header
            writer.WriteStartElement("u", "Timestamp", Namespaces.WSSecurityUtility);
            writer.WriteAttributeString("u", "Id", Namespaces.WSSecurityUtility, "_0");
            writer.WriteStartElement("u", "Created", Namespaces.WSSecurityUtility);
            writer.WriteString(((XmlText)created.FirstChild).Value);
            writer.WriteEndElement(); // u:Created
            writer.WriteStartElement("u", "Expires", Namespaces.WSSecurityUtility);
            writer.WriteString(((XmlText)expires.FirstChild).Value);
            writer.WriteEndElement(); // u:Expires
            writer.WriteEndElement(); // u:Timestamp

            // Write the details of the security context we'll be signing it with
            writer.WriteStartElement("c", "SecurityContextToken", Namespaces.WSSecureConversation);
            writer.WriteAttributeString("u", "Id", Namespaces.WSSecurityUtility, _securityContextToken.Id);
            writer.WriteStartElement("c", "Identifier", Namespaces.WSSecureConversation);
            writer.WriteString(_securityContextToken.Identifier);
            writer.WriteEndElement(); // c:Identifier
            writer.WriteEndElement(); // c:SecurityContextToken

            // 3. Canonicalize the timestamp and generate the digest
            var timestampDigest = GetDigest(timestampDoc);

            // 4. Build the SignedInfo document:
            // <SignedInfo xmlns="http://www.w3.org/2000/09/xmldsig#">
            //   <CanonicalizationMethod Algorithm="http://www.w3.org/2001/10/xml-exc-c14n#" />
            //   <SignatureMethod Algorithm="http://www.w3.org/2000/09/xmldsig#hmac-sha1" />
            //   <Reference URI="#_0">
            //     <Transforms>
            //       <Transform Algorithm="http://www.w3.org/2001/10/xml-exc-c14n#" />
            //     </Transforms>
            //     <DigestMethod Algorithm="http://www.w3.org/2000/09/xmldsig#sha1" />
            //     <DigestValue>{base64:timestampDigest}</DigestValue>
            //   </Reference>
            // </SignedInfo>
            var signatureDoc = new XmlDocument();
            signatureDoc.PreserveWhitespace = false;
            var signedInfo = signatureDoc.CreateElement("SignedInfo", "http://www.w3.org/2000/09/xmldsig#");
            signatureDoc.AppendChild(signedInfo);
            var c14nMethod = signatureDoc.CreateElement("CanonicalizationMethod", "http://www.w3.org/2000/09/xmldsig#");
            c14nMethod.SetAttribute("Algorithm", "http://www.w3.org/2001/10/xml-exc-c14n#");
            signedInfo.AppendChild(c14nMethod);
            var sigMethod = signatureDoc.CreateElement("SignatureMethod", "http://www.w3.org/2000/09/xmldsig#");
            sigMethod.SetAttribute("Algorithm", "http://www.w3.org/2000/09/xmldsig#hmac-sha1");
            signedInfo.AppendChild(sigMethod);
            var reference = signatureDoc.CreateElement("Reference", "http://www.w3.org/2000/09/xmldsig#");
            reference.SetAttribute("URI", "#_0");
            signedInfo.AppendChild(reference);
            var transforms = signatureDoc.CreateElement("Transforms", "http://www.w3.org/2000/09/xmldsig#");
            reference.AppendChild(transforms);
            var transform = signatureDoc.CreateElement("Transform", "http://www.w3.org/2000/09/xmldsig#");
            transform.SetAttribute("Algorithm", "http://www.w3.org/2001/10/xml-exc-c14n#");
            transforms.AppendChild(transform);
            var digestMethod = signatureDoc.CreateElement("DigestMethod", "http://www.w3.org/2000/09/xmldsig#");
            digestMethod.SetAttribute("Algorithm", "http://www.w3.org/2000/09/xmldsig#sha1");
            reference.AppendChild(digestMethod);
            var digestValue = signatureDoc.CreateElement("DigestValue", "http://www.w3.org/2000/09/xmldsig#");
            reference.AppendChild(digestValue);
            digestValue.AppendChild(signatureDoc.CreateTextNode(Convert.ToBase64String(timestampDigest)));

            // 5. Create the signature
            var signatureHash = GetSignature(signatureDoc, _proofToken);

            // 6. Wrap the SignedInfo in a Signature
            writer.WriteStartElement(null, "Signature", Namespaces.XmlSignature);
            CopyXmlDoc(signatureDoc.DocumentElement, writer);
            writer.WriteStartElement("SignatureValue");
            writer.WriteString(Convert.ToBase64String(signatureHash));
            writer.WriteEndElement(); // SignatureValue

            writer.WriteStartElement("KeyInfo");
            writer.WriteStartElement("o", "SecurityTokenReference", Namespaces.WSSecurityExtensions);
            writer.WriteStartElement("o", "Reference", Namespaces.WSSecurityExtensions);
            writer.WriteAttributeString("ValueType", "http://schemas.xmlsoap.org/ws/2005/02/sc/sct");
            writer.WriteAttributeString("URI", "#" + _securityContextToken.Id);
            writer.WriteEndElement(); // o:Reference
            writer.WriteEndElement(); // o:SecurityTokenReference
            writer.WriteEndElement(); // KeyInfo

            writer.WriteEndElement(); // Signature
        }

        private byte[] GetDigest(XmlDocument xmlDoc)
        {
            // https://stackoverflow.com/questions/27367034/creating-signed-soap-message-as-a-string-with-c-sharp
            var trans = new XmlDsigExcC14NTransform();
            trans.LoadInput(xmlDoc);
            var hash = trans.GetDigestedOutput(SHA1.Create());
            return hash;
        }

        private byte[] GetSignature(XmlDocument xmlDoc, byte[] key)
        {
            // https://stackoverflow.com/questions/27367034/creating-signed-soap-message-as-a-string-with-c-sharp
            var trans = new XmlDsigC14NTransform();
            trans.LoadInput(xmlDoc);
            var hash = trans.GetDigestedOutput(new HMACSHA1(key));
            return hash;
        }

        private void CopyXmlDoc(XmlElement element, XmlDictionaryWriter writer)
        {
            writer.WriteStartElement(element.Prefix, element.LocalName, element.NamespaceURI);

            foreach (XmlAttribute attr in element.Attributes)
                writer.WriteAttributeString(attr.Prefix, attr.LocalName, attr.NamespaceURI, attr.Value);

            foreach (var child in element.ChildNodes)
            {
                if (child is XmlText text)
                    writer.WriteString(text.Value);
                else if (child is XmlElement childElement)
                    CopyXmlDoc(childElement, writer);
                else
                    throw new NotSupportedException();
            }

            writer.WriteEndElement();
        }
    }
}
