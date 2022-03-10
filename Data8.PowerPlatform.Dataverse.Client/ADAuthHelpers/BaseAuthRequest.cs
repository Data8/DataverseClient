using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;

namespace Data8.PowerPlatform.Dataverse.Client.ADAuthHelpers
{
    abstract class BaseAuthRequest : BodyWriter
    {
        protected BaseAuthRequest() : base(isBuffered: true)
        {
        }

        protected abstract string Action { get; }

        public object Execute(string url, Authenticator auth)
        {
            // Add the request to the hash for later authentication
            var doc = new XmlDocument();
            using (var writer = doc.CreateNavigator().AppendChild())
            {
                WriteBodyContents(XmlDictionaryWriter.CreateDictionaryWriter(writer));
            }
            auth.AddToDigest(doc);

            // Create the SOAP message to send the request
            var message = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, Action, this);
            message.Headers.MessageId = new UniqueId(Guid.NewGuid());
            message.Headers.ReplyTo = new EndpointAddress("http://www.w3.org/2005/08/addressing/anonymous");
            message.Headers.To = new Uri(url);

            var req = WebRequest.CreateHttp(url);
            req.Method = "POST";
            req.ContentType = "application/soap+xml; charset=utf-8";

            using (var reqStream = req.GetRequestStream())
            using (var xmlTextWriter = XmlWriter.Create(reqStream, new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false,
                Encoding = new UTF8Encoding(false),
                CloseOutput = true
            }))
            using (var xmlWriter = XmlDictionaryWriter.CreateDictionaryWriter(xmlTextWriter))
            {
                message.WriteMessage(xmlWriter);
                xmlWriter.WriteEndDocument();
                xmlWriter.Flush();
            }

            try
            {
                using (var resp = req.GetResponse())
                using (var respStream = resp.GetResponseStream())
                {
                    var reader = XmlReader.Create(respStream, new XmlReaderSettings());
                    var responseMessage = Message.CreateMessage(reader, 0x10000, MessageVersion.Soap12WSAddressing10);
                    var responseAction = responseMessage.Headers.Action;

                    using (var bodyReader = responseMessage.GetReaderAtBodyContents())
                    {
                        // Read the response as the appropriate type
                        if (bodyReader.LocalName == "RequestSecurityTokenResponse")
                            return RequestSecurityTokenResponse.Read(bodyReader, auth, false);
                        else if (bodyReader.LocalName == "RequestSecurityTokenResponseCollection")
                            return RequestSecurityTokenResponseCollection.Read(bodyReader, auth);
                        else
                            throw new NotSupportedException("Unexpected response element " + bodyReader.LocalName);
                    }
                }
            }
            catch (WebException ex)
            {
                using (var errorStream = ex.Response.GetResponseStream())
                {
                    var reader = XmlReader.Create(errorStream, new XmlReaderSettings());
                    var responseMessage = Message.CreateMessage(reader, 0x10000, MessageVersion.Soap12WSAddressing10);
                    var responseAction = responseMessage.Headers.Action;

                    using (var bodyReader = responseMessage.GetReaderAtBodyContents())
                    {
                        throw FaultReader.ReadFault(bodyReader, responseAction);
                    }
                }
            }
        }
    }
}
