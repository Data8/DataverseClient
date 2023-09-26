using System;
using System.Threading.Tasks;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Xunit;
using Xunit.Abstractions;
using AuthenticationType = Data8.PowerPlatform.Dataverse.Client.Wsdl.AuthenticationType;

namespace Data8.PowerPlatform.Dataverse.Client.Tests
{
    public class AdAuthClientTests
    {
        private readonly ITestOutputHelper _output;

        private string AdUrl { get; init; }
        private string AdUsername { get; init; }
        private string AdPassword { get; init; }

        public AdAuthClientTests(ITestOutputHelper output)
        {
            _output = output;
            //allow all certificates
            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

             AdUrl = Environment.GetEnvironmentVariable("AD_URL") ?? throw new Exception("AD_URL environment variable is not set");
             AdUsername = Environment.GetEnvironmentVariable("AD_USERNAME") ?? throw new Exception("AD_USERNAME environment variable is not set");
             AdPassword = Environment.GetEnvironmentVariable("AD_PASSWORD") ?? throw new Exception("AD_PASSWORD environment variable is not set");
        }

        #region SYNC TESTS 

        [Fact]
        public void WhoAmIRequestTest()
        {
            var client = new OnPremiseClient(AdUrl, AdUsername, AdPassword);
            Assert.Equal(AuthenticationType.ActiveDirectory, client.AuthenticationType);
            var response = client.Execute(new WhoAmIRequest()) as WhoAmIResponse;

            Assert.NotNull(response);
            Assert.NotEqual(Guid.Empty, response.UserId);
        }

        [Fact]
        public void CloneTest()
        {
            var client = new OnPremiseClient(AdUrl, AdUsername, AdPassword);
            Assert.Equal(AuthenticationType.ActiveDirectory, client.AuthenticationType);
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
            var client1 = new OnPremiseClient(AdUrl, AdUsername, AdPassword);
            Assert.Equal(AuthenticationType.ActiveDirectory, client1.AuthenticationType);
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
            var client = new OnPremiseClient(AdUrl, AdUsername, AdPassword);
            Assert.Equal(AuthenticationType.ActiveDirectory, client.AuthenticationType);
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
            var entityName = Environment.GetEnvironmentVariable("AD_ENTITY_NAME") ?? throw new Exception("AD_ENTITY_NAME environment variable is not set");
            var id = Guid.Parse(Environment.GetEnvironmentVariable("AD_ENTITY_ID") ?? throw new Exception("AD_ENTITY_ID environment variable is not set"));

            var columnName = Environment.GetEnvironmentVariable("AD_COLUMN_NAME") ?? throw new Exception("AD_COLUMN_NAME environment variable is not set");
            var columnValue = Environment.GetEnvironmentVariable("AD_COLUMN_VALUE") ?? throw new Exception("AD_COLUMN_VALUE environment variable is not set");

            var client = new OnPremiseClient(AdUrl, AdUsername, AdPassword);

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

        #endregion

        #region ASYNC TESTS

        [Fact]
        public async Task WhoAmIRequestAsyncTest()
        {
            var client = new OnPremiseClient(AdUrl, AdUsername, AdPassword);
            Assert.Equal(AuthenticationType.ActiveDirectory, client.AuthenticationType);
            var response = (await client.ExecuteAsync(new WhoAmIRequest())) as WhoAmIResponse;

            Assert.NotNull(response);
            Assert.NotEqual(Guid.Empty, response.UserId);
        }

        [Fact]
        public void UpdateAndRetrieveAsyncInTasksTest()
        {
            var entityName = Environment.GetEnvironmentVariable("AD_ENTITY_NAME") ?? throw new Exception("AD_ENTITY_NAME environment variable is not set");
            var id = Guid.Parse(Environment.GetEnvironmentVariable("AD_ENTITY_ID") ?? throw new Exception("AD_ENTITY_ID environment variable is not set"));

            var columnName = Environment.GetEnvironmentVariable("AD_COLUMN_NAME") ?? throw new Exception("AD_COLUMN_NAME environment variable is not set");
            var columnValue = Environment.GetEnvironmentVariable("AD_COLUMN_VALUE") ?? throw new Exception("AD_COLUMN_VALUE environment variable is not set");

            var client = new OnPremiseClient(AdUrl, AdUsername, AdPassword);
            Assert.Equal(AuthenticationType.ActiveDirectory, client.AuthenticationType);

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

        #endregion
    }
}
