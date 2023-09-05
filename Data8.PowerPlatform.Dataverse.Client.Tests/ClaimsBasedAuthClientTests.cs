using Microsoft.Crm.Sdk.Messages;
using System;
using Xunit;
using Xunit.Abstractions;

namespace Data8.PowerPlatform.Dataverse.Client.Tests
{
    public class ClaimsBasedAuthClientTests
    {
        private readonly ITestOutputHelper _output;

        private string ClaimsUrl { get; init; }
        private string ClaimsUsername { get; init; }
        private string ClaimsPassword { get; init; }

        internal ClaimsBasedAuthClientTests(ITestOutputHelper output)
        {
            _output = output;
            //allow all certificates
            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            ClaimsUrl = Environment.GetEnvironmentVariable("CLAIMS_URL") ?? throw new Exception("CLAIMS_URL environment variable is not set");
            ClaimsUsername = Environment.GetEnvironmentVariable("CLAIMS_USERNAME") ?? throw new Exception("CLAIMS_USERNAME environment variable is not set");
            ClaimsPassword = Environment.GetEnvironmentVariable("CLAIMS_PASSWORD") ?? throw new Exception("CLAIMS_PASSWORD environment variable is not set");
        }

        [Fact]
        public void ClaimsTest()
        {
            var client = new OnPremiseClient(ClaimsUrl, ClaimsUsername, ClaimsPassword);
            client.Execute(new WhoAmIRequest());
        }
    }
}
