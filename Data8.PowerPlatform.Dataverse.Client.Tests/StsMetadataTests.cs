using Data8.PowerPlatform.Dataverse.Client.Wsdl;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace Data8.PowerPlatform.Dataverse.Client.Tests
{
    /// <summary>
    /// Offline tests for discovering the username WS-Trust endpoint from STS metadata.
    /// These do not require a live server.
    /// </summary>
    public class StsMetadataTests
    {
        private static string ReadTestData(string fileName)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", fileName));
        }

        private static Definitions Parse(string xml)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                return WsdlLoader.Parse(stream);
            }
        }

        [Fact]
        public void FindsUsernameEndpointWithWsPolicy2004AndSignedEncryptedSupportingTokens()
        {
            var wsdl = Parse(ReadTestData("wspolicy2004-signedencrypted-mex.xml"));

            var port = OnPremiseClient.FindUsernameWsTrust13Port(new[] { wsdl });

            Assert.Equal("https://sts.contoso.com/adfs/services/trust/13/usernamemixed", port.Address.Location);
        }

        [Fact]
        public void FindsUsernameEndpointWithWsPolicy15AndSignedSupportingTokens()
        {
            var wsdl = Parse(ReadTestData("wspolicy15-usernameovertransport-mex.xml"));

            var port = OnPremiseClient.FindUsernameWsTrust13Port(new[] { wsdl });

            Assert.Equal("https://sts.contoso.com/trust/usernamemixed", port.Address.Location);
        }

        [Fact]
        public void FindsUsernameEndpointRegardlessOfBindingNamespacePrefix()
        {
            var xml = ReadTestData("wspolicy15-usernameovertransport-mex.xml")
                .Replace("xmlns:tns=", "xmlns:sts=")
                .Replace("\"tns:", "\"sts:");
            var wsdl = Parse(xml);

            var port = OnPremiseClient.FindUsernameWsTrust13Port(new[] { wsdl });

            Assert.Equal("https://sts.contoso.com/trust/usernamemixed", port.Address.Location);
        }

        [Fact]
        public void ThrowsWhenNoUsernameEndpointIsPublished()
        {
            var xml = ReadTestData("wspolicy2004-signedencrypted-mex.xml")
                .Replace("<sp:UsernameToken", "<sp:X509Token")
                .Replace("</sp:UsernameToken>", "</sp:X509Token>");
            var wsdl = Parse(xml);

            var ex = Assert.Throws<InvalidOperationException>(() => OnPremiseClient.FindUsernameWsTrust13Port(new[] { wsdl }, "https://sts.contoso.com/mex"));

            Assert.Contains("username token endpoint", ex.Message);
            Assert.Contains("https://sts.contoso.com/mex", ex.Message);
        }
    }
}
