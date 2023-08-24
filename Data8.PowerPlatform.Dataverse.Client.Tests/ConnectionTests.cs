using System;
using System.Threading.Tasks;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Xunit;
using Xunit.Abstractions;

namespace Data8.PowerPlatform.Dataverse.Client.Tests
{
    public class ConnectionTests
    {
        private readonly ITestOutputHelper _output;

        public ConnectionTests(ITestOutputHelper output)
        {
            _output = output;
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

            client = null;

            var resp2 = (WhoAmIResponse)newClient.Execute(new WhoAmIRequest());

            Assert.Equal(resp.UserId, resp2.UserId);
        }

        [Fact]
        public void CloneACloneTest()
        {
            var adUrl = Environment.GetEnvironmentVariable("AD_URL");
            var adUsername = Environment.GetEnvironmentVariable("AD_USERNAME");
            var adPassword = Environment.GetEnvironmentVariable("AD_PASSWORD");

            var client = new OnPremiseClient(adUrl, adUsername, adPassword);
            var resp = (WhoAmIResponse)client.Execute(new WhoAmIRequest());

            var newClient = client.Clone();

            client = null;

            var resp2 = (WhoAmIResponse)newClient.Execute(new WhoAmIRequest());

            Assert.Equal(resp.UserId, resp2.UserId);

            var newClient2 = newClient.Clone();
            var resp3 = (WhoAmIResponse)newClient2.Execute(new WhoAmIRequest());

            Assert.Equal(resp.UserId, resp3.UserId);
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
        public void UpdateTestInTasks()
        {
            var id = Guid.Parse("36f57237-5187-e311-82a1-002219bd3fb2");

            var adUrl = Environment.GetEnvironmentVariable("AD_URL");
            var adUsername = Environment.GetEnvironmentVariable("AD_USERNAME");
            var adPassword = Environment.GetEnvironmentVariable("AD_PASSWORD");

            var client = new OnPremiseClient(adUrl, adUsername, adPassword);

            var tasks = new Task[100];

            for (var i = 0; i < tasks.Length; i++)
            {
                var j = i;
                tasks[i] = Task.Run(async () =>
                {
                    var newClient = client.Clone();
                    var en = new Entity("tes_object", id)
                    {
                        ["test_column"] = "TEST"
                    };

                    await newClient.UpdateAsync(en);

                    var entity = await newClient.RetrieveAsync(en.LogicalName,en.Id, new Microsoft.Xrm.Sdk.Query.ColumnSet("test_column"));
                    _output.WriteLine($"{j}-{entity["test_column"]}");
                });
            }

            //await all tasks
            Task.WaitAll(tasks);
        }

        [Fact]
        public async Task ADTestAsync()
        {
            var adUrl = Environment.GetEnvironmentVariable("AD_URL");
            var adUsername = Environment.GetEnvironmentVariable("AD_USERNAME");
            var adPassword = Environment.GetEnvironmentVariable("AD_PASSWORD");

            var client = new OnPremiseClient(adUrl, adUsername, adPassword);
            var response = await client.ExecuteAsync(new WhoAmIRequest());
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
