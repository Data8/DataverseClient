using Data8.PowerPlatform.Dataverse.Client.Wsdl;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using System;
using System.Security.Policy;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using AuthenticationType = Data8.PowerPlatform.Dataverse.Client.Wsdl.AuthenticationType;

namespace Data8.PowerPlatform.Dataverse.Client.Tests
{
    public class ClaimsBasedAuthClientTests
    {
        private readonly ITestOutputHelper _output;

        private string ClaimsUrl { get; init; }
        private string ClaimsUsername { get; init; }
        private string ClaimsPassword { get; init; }

        public ClaimsBasedAuthClientTests(ITestOutputHelper output)
        {
            _output = output;
            //allow all certificates
            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            ClaimsUrl = Environment.GetEnvironmentVariable("CLAIMS_URL") ?? throw new Exception("CLAIMS_URL environment variable is not set");
            ClaimsUsername = Environment.GetEnvironmentVariable("CLAIMS_USERNAME") ?? throw new Exception("CLAIMS_USERNAME environment variable is not set");
            ClaimsPassword = Environment.GetEnvironmentVariable("CLAIMS_PASSWORD") ?? throw new Exception("CLAIMS_PASSWORD environment variable is not set");
        }

        #region SYNC TESTS 

        [Fact]
        public void WhoAmIRequestTest()
        {
            var client = new OnPremiseClient(ClaimsUrl, ClaimsUsername, ClaimsPassword);
            Assert.Equal(AuthenticationType.Federation, client.AuthenticationType);
            var response = client.Execute(new WhoAmIRequest()) as WhoAmIResponse;

            Assert.NotNull(response);
            Assert.NotEqual(Guid.Empty, response.UserId);
        }

        [Fact]
        public void CloneTest()
        {
            var client = new OnPremiseClient(ClaimsUrl, ClaimsUsername, ClaimsPassword);
            Assert.Equal(AuthenticationType.Federation, client.AuthenticationType);
            var response1 = client.Execute(new WhoAmIRequest()) as WhoAmIResponse;
            var newClient = client.Clone();
            client = null;
            var response2 = newClient.Execute(new WhoAmIRequest()) as WhoAmIResponse;

            Assert.NotNull(response1);
            Assert.NotNull(response2);
            Assert.Equal(response1.UserId, response2.UserId);
        }

        [Fact]
        public void CloneACloneTest()
        {
            var client1 = new OnPremiseClient(ClaimsUrl, ClaimsUsername, ClaimsPassword);
            Assert.Equal(AuthenticationType.Federation, client1.AuthenticationType);
            var response1 = client1.Execute(new WhoAmIRequest()) as WhoAmIResponse;

            var client2 = client1.Clone();
            client1 = null;
            var response2 = client2.Execute(new WhoAmIRequest()) as WhoAmIResponse;

            var client3 = client2.Clone();
            client2 = null;
            var response3 = client3.Execute(new WhoAmIRequest()) as WhoAmIResponse;

            Assert.NotNull(response1);
            Assert.NotNull(response2);
            Assert.NotNull(response3);
            Assert.Equal(response1.UserId, response2.UserId);
            Assert.Equal(response1.UserId, response3.UserId);
        }

        [Fact]
        public void CloneTestInTasks()
        {
            var client = new OnPremiseClient(ClaimsUrl, ClaimsUsername, ClaimsPassword);
            Assert.Equal(AuthenticationType.Federation, client.AuthenticationType);
            var response1 = client.Execute(new WhoAmIRequest()) as WhoAmIResponse;

            Assert.NotNull(response1);

            var tasks = new Task[10];

            for (var i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    var newClient = client.Clone();
                    var response2 = (WhoAmIResponse)newClient.Execute(new WhoAmIRequest());

                    Assert.Equal(response1.UserId, response2.UserId);
                });
            }

            Task.WaitAll(tasks);
        }

        [Fact]
        public void UpdateAndRetrieveInTasksTest()
        {
            var entityName = Environment.GetEnvironmentVariable("CLAIMS_ENTITY_NAME") ?? throw new Exception("CLAIMS_ENTITY_NAME environment variable is not set");

            var columnName = Environment.GetEnvironmentVariable("CLAIMS_COLUMN_NAME") ?? throw new Exception("CLAIMS_COLUMN_NAME environment variable is not set");
            var columnValue = Environment.GetEnvironmentVariable("CLAIMS_COLUMN_VALUE") ?? throw new Exception("CLAIMS_COLUMN_VALUE environment variable is not set");

            var client = new OnPremiseClient(ClaimsUrl, ClaimsUsername, ClaimsPassword);

            var id = client.Create(new Entity(entityName));

            try
            {
                var tasks = new Task[10];

                for (var i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        var newClient = client.Clone();
                        var en = new Entity(entityName, id)
                        {
                            [columnName] = columnValue
                        };

                        newClient.Update(en);

                        var entity = newClient.Retrieve(en.LogicalName, en.Id, new Microsoft.Xrm.Sdk.Query.ColumnSet(columnName));
                        _output.WriteLine($"{entity[columnName]}");
                    });
                }

                Task.WaitAll(tasks);
            }
            finally
            {
                client.Delete(entityName, id);
            }
        }

        #endregion

        #region ASYNC TESTS

        [Fact]
        public async Task WhoAmIRequestAsyncTest()
        {
            var client = new OnPremiseClient(ClaimsUrl, ClaimsUsername, ClaimsPassword);
            Assert.Equal(AuthenticationType.Federation, client.AuthenticationType);
            var response = (await client.ExecuteAsync(new WhoAmIRequest())) as WhoAmIResponse;

            Assert.NotNull(response);
            Assert.NotEqual(Guid.Empty, response.UserId);
        }

        [Fact]
        public async void UpdateAndRetrieveAsyncInTasksTest()
        {
            var entityName = Environment.GetEnvironmentVariable("CLAIMS_ENTITY_NAME") ?? throw new Exception("CLAIMS_ENTITY_NAME environment variable is not set");

            var columnName = Environment.GetEnvironmentVariable("CLAIMS_COLUMN_NAME") ?? throw new Exception("CLAIMS_COLUMN_NAME environment variable is not set");
            var columnValue = Environment.GetEnvironmentVariable("CLAIMS_COLUMN_VALUE") ?? throw new Exception("CLAIMS_COLUMN_VALUE environment variable is not set");

            var client = new OnPremiseClient(ClaimsUrl, ClaimsUsername, ClaimsPassword);
            Assert.Equal(AuthenticationType.Federation, client.AuthenticationType);

            var id = await client.CreateAsync(new Entity(entityName));

            try
            {
                var tasks = new Task[10];

                for (var i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = Task.Run(async () =>
                    {
                        var newClient = client.Clone();
                        var en = new Entity(entityName, id)
                        {
                            [columnName] = columnValue
                        };

                        await newClient.UpdateAsync(en);

                        var entity = await newClient.RetrieveAsync(en.LogicalName, en.Id, new Microsoft.Xrm.Sdk.Query.ColumnSet(columnName));
                        _output.WriteLine($"{entity[columnName]}");
                    });
                }

                Task.WaitAll(tasks);
            }
            finally
            {
                await client.DeleteAsync(entityName, id);
            }
        }

        #endregion
    }
}
