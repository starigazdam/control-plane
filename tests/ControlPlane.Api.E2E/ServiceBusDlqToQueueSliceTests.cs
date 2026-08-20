using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ControlPlane.Api.E2E;

public sealed class ServiceBusDlqToQueueSliceTests
{
    [Fact]
    public async Task ServiceBusDlqToQueueFlow_is_exposed_and_executable()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ControlPlane");
        var previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        try
        {
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__ControlPlane",
                $"Data Source={tempDbPath}");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            {
                await using var factory = new WebApplicationFactory<Program>();
                using var client = factory.CreateClient();

                var projectResponse = await client.GetAsync("/api/projects/placeholder");
                Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);

                var projectJson = await projectResponse.Content.ReadFromJsonAsync<JsonDocument>();
                Assert.NotNull(projectJson);

                var operations = projectJson.RootElement.GetProperty("operations");
                Assert.Contains(operations.EnumerateArray(), operation =>
                    operation.GetProperty("id").GetString() == "resend-servicebus-dlq-to-queue");

                var executeResponse = await client.PostAsJsonAsync(
                    "/api/operations/execute",
                    new
                    {
                        projectId = "placeholder",
                        operationId = "resend-servicebus-dlq-to-queue",
                        input = new
                        {
                            sourceDlqPath = "orders/$DeadLetterQueue",
                            queueName = "orders"
                        },
                        requestedBy = "e2e-test"
                    });

                if (executeResponse.StatusCode != HttpStatusCode.OK)
                {
                    var errorBody = await executeResponse.Content.ReadAsStringAsync();
                    Assert.Fail($"Execute returned {(int)executeResponse.StatusCode}: {errorBody}");
                }

                var executionJson = await executeResponse.Content.ReadFromJsonAsync<JsonDocument>();
                Assert.NotNull(executionJson);
                Assert.Equal(
                    "resend-servicebus-dlq-to-queue",
                    executionJson.RootElement.GetProperty("operationId").GetString());
                Assert.Equal(
                    "Succeeded",
                    executionJson.RootElement
                        .GetProperty("result")
                        .GetProperty("status")
                        .GetString());

                var historyResponse = await client.GetAsync("/api/operations/history?take=5");
                if (historyResponse.StatusCode != HttpStatusCode.OK)
                {
                    var errorBody = await historyResponse.Content.ReadAsStringAsync();
                    Assert.Fail($"History returned {(int)historyResponse.StatusCode}: {errorBody}");
                }

                var historyJson = await historyResponse.Content.ReadFromJsonAsync<JsonDocument>();
                Assert.NotNull(historyJson);

                Assert.Contains(historyJson.RootElement.EnumerateArray(), entry =>
                    entry.GetProperty("operationId").GetString() == "resend-servicebus-dlq-to-queue");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__ControlPlane", previousConnectionString);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
        }
    }
}
