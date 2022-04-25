using System;
using Microsoft.Crm.Sdk.Messages;
using Xunit;

namespace Data8.PowerPlatform.Dataverse.Client.Tests
{
    public class ConnectionTests
    {
        [Fact]
        public void ADTest()
        {
            var adUrl = Environment.GetEnvironmentVariable("AD_URL");
            var adUsername = Environment.GetEnvironmentVariable("AD_USERNAME");
            var adPassword = Environment.GetEnvironmentVariable("AD_PASSWORD");

            var client = new OnPremiseClient(adUrl, adUsername, adPassword);
            client.Execute(new WhoAmIRequest());
        }

        [Fact]
        public void ClaimsTest()
        {
            var claimsUrl = Environment.GetEnvironmentVariable("CLAIMS_URL");
            var claimsUsername = Environment.GetEnvironmentVariable("CLAIMS_USERNAME");
            var claimsPassword = Environment.GetEnvironmentVariable("CLAIMS_PASSWORD");

            var client = new OnPremiseClient(claimsUrl, claimsUsername, claimsPassword);
            client.Execute(new WhoAmIRequest());
        }
    }
}
