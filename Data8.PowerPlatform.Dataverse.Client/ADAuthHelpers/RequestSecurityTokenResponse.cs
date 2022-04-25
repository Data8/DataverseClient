using System;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    class RequestSecurityTokenResponse : BaseAuthRequest
    {
        public RequestSecurityTokenResponse(string context, byte[] token)
        {
            if (string.IsNullOrEmpty(context))
                throw new ArgumentNullException(nameof(context));

            if (token == null)
                throw new ArgumentNullException(nameof(token));

            Context = context;
            BinaryExchange = new BinaryExchange(token);
        }

        private RequestSecurityTokenResponse()
        {
        }

        protected override string Action => "http://schemas.xmlsoap.org/ws/2005/02/trust/RSTR/Issue";

        public string Context { get; private set; }

        public string TokenType { get; private set; }

        public SecurityContextToken RequestedSecurityToken { get; private set; }

        public SecurityTokenReference RequestedAttachedReference { get; private set; }

        public SecurityTokenReference RequestedUnattachedReference { get; private set; }

        public EncryptedKey RequestedProofToken { get; private set; }

        public Lifetime Lifetime { get; private set; }

        public int? KeySize { get; private set; }

        public BinaryExchange BinaryExchange { get; private set; }

        public CombinedHash Authenticator { get; private set; }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("t", nameof(RequestSecurityTokenResponse), Namespaces.WSTrust);
            writer.WriteAttributeString(nameof(Context), Context);

            BinaryExchange.WriteBodyContents(writer);

            writer.WriteEndElement(); // t:RequestSecurityTokenResponse
        }

        public static RequestSecurityTokenResponse Read(XmlDictionaryReader reader, Authenticator auth, bool isFinal)
        {
            if (reader.LocalName != nameof(RequestSecurityTokenResponse) || reader.NamespaceURI != Namespaces.WSTrust)
                throw new InvalidOperationException();

            if (auth != null)
            {
                // Add the response to the hash
                // For the final response, exclude the RequestedSecurityToken and RequestedProofToken elements
                var subtree = reader.ReadSubtree();
                var doc = new XmlDocument();
                doc.Load(subtree);
                reader.ReadEndElement();

                if (isFinal)
                {
                    var clone = (XmlDocument) doc.Clone();
                    var rst = clone.SelectSingleNode("//*[local-name()='RequestedSecurityToken']");
                    var rpt = clone.SelectSingleNode("//*[local-name()='RequestedProofToken']");

                    rst?.ParentNode?.RemoveChild(rst);
                    rpt?.ParentNode?.RemoveChild(rpt);

                    auth.AddToDigest(clone);
                }
                else
                {
                    auth.AddToDigest(doc);
                }

                reader = XmlDictionaryReader.CreateDictionaryReader(new XmlNodeReader(doc));
                reader.MoveToContent();
            }

            var rstr = new RequestSecurityTokenResponse();
            rstr.Context = reader.GetAttribute(nameof(Context));
            reader.ReadStartElement(nameof(RequestSecurityTokenResponse), Namespaces.WSTrust);

            while (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.NamespaceURI != Namespaces.WSTrust)
                {
                    reader.ReadSubtree();
                    continue;
                }

                switch (reader.LocalName)
                {
                    case nameof(TokenType):
                        reader.ReadStartElement();
                        rstr.TokenType = reader.ReadString();
                        reader.ReadEndElement();
                        break;

                    case nameof(RequestedSecurityToken):
                        reader.ReadStartElement();
                        rstr.RequestedSecurityToken = SecurityContextToken.Read(reader);
                        reader.ReadEndElement();
                        break;

                    case nameof(RequestedAttachedReference):
                        reader.ReadStartElement();
                        rstr.RequestedAttachedReference = SecurityTokenReference.Read(reader);
                        reader.ReadEndElement();
                        break;

                    case nameof(RequestedUnattachedReference):
                        reader.ReadStartElement();
                        rstr.RequestedUnattachedReference = SecurityTokenReference.Read(reader);
                        reader.ReadEndElement();
                        break;

                    case nameof(RequestedProofToken):
                        reader.ReadStartElement();
                        rstr.RequestedProofToken = EncryptedKey.Read(reader);
                        reader.ReadEndElement();
                        break;

                    case nameof(Lifetime):
                        reader.ReadStartElement();
                        rstr.Lifetime = Lifetime.Read(reader);
                        reader.ReadEndElement();
                        break;

                    case nameof(KeySize):
                        reader.ReadStartElement();
                        rstr.KeySize = reader.ReadContentAsInt();
                        reader.ReadEndElement();
                        break;

                    case nameof(BinaryExchange):
                        rstr.BinaryExchange = BinaryExchange.Read(reader);
                        break;

                    case nameof(Authenticator):
                        reader.ReadStartElement();
                        rstr.Authenticator = CombinedHash.Read(reader);
                        reader.ReadEndElement();
                        break;

                    default:
                        reader.ReadSubtree();
                        break;
                }
            }

            reader.ReadEndElement(); // t:RequestSecurityTokenResponse
            return rstr;
        }
    }
}
