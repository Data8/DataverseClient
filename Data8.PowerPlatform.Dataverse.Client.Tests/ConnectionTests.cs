using System;
using System.Threading.Tasks;
using Microsoft.Crm.Sdk.Messages;
using Xunit;

namespace Data8.PowerPlatform.Dataverse.Client.Tests
{
    public class ConnectionTests
    {
        public ConnectionTests()
        {
            //allow all certificates
            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        [Fact]
        public void ADTest()
        {
            var adUrl = Environment.GetEnvironmentVariable("AD_URL");
            var adUsername = Environment.GetEnvironmentVariable("AD_USERNAME");
            var adPassword = Environment.GetEnvironmentVariable("AD_PASSWORD");

            var client = new OnPremiseClient(adUrl, adUsername, adPassword);
            var resp = (WhoAmIResponse)client.Execute(new WhoAmIRequest());

            Assert.NotEqual(Guid.Empty, resp.UserId);
        }

        [Fact]
        public void CloneTest()
        {
            var adUrl = Environment.GetEnvironmentVariable("AD_URL");
            var adUsername = Environment.GetEnvironmentVariable("AD_USERNAME");
            var adPassword = Environment.GetEnvironmentVariable("AD_PASSWORD");

            var client = new OnPremiseClient(adUrl, adUsername, adPassword);
            var resp = (WhoAmIResponse)client.Execute(new WhoAmIRequest());

            var newClient = client.Clone();

            client.Dispose();
            client = null;

            var resp2 = (WhoAmIResponse)newClient.Execute(new WhoAmIRequest());

            Assert.Equal(resp.UserId, resp2.UserId);
        }

        [Fact]
        public void CloneTestInTasks()
        {
            var adUrl = Environment.GetEnvironmentVariable("AD_URL");
            var adUsername = Environment.GetEnvironmentVariable("AD_USERNAME");
            var adPassword = Environment.GetEnvironmentVariable("AD_PASSWORD");

            var client = new OnPremiseClient(adUrl, adUsername, adPassword);
            var resp = (WhoAmIResponse)client.Execute(new WhoAmIRequest());

            var tasks = new Task[100];
            //in each task clone the client and execute a whoami request
            for (var i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    var newClient = client.Clone();
                    var resp2 = (WhoAmIResponse)newClient.Execute(new WhoAmIRequest());
                    Assert.Equal(resp.UserId, resp2.UserId);
                });
            }

            //await all tasks
            Task.WaitAll(tasks);
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
