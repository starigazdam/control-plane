using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
namespace ControlPlane.Api.E2E;
/// <summary>
/// In-process observe-and-act slice: fabricated loopback endpoint, overview status,
/// <c>http-endpoint-check</c> execution, and safe persisted history. No live endpoint.
/// </summary>
public sealed class HttpEndpointCheckSliceTests
{
    [Fact]
    public async Task HttpEndpointCheckFlow_is_exposed_and_executable()
    {
        using var server = new LoopbackHttpServer();
        var configPath = Path.Combine(Path.GetTempPath(), $"http-endpoint-e2e-{Guid.NewGuid():N}.json");
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new[]
        {
            new { id = "loopback-health", name = "Loopback health", url = server.Url, expectedStatus = 200, method = "GET", timeoutSeconds = 5 }
        }));
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ControlPlane");
        var previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var previousConfigPath = Environment.GetEnvironmentVariable("HttpEndpoint__ConfigPath");
        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__ControlPlane", $"Data Source={tempDbPath}");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("HttpEndpoint__ConfigPath", configPath);
            await using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            var projectResponse = await client.GetAsync("/api/projects/placeholder");
            Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);
            var projectJson = await projectResponse.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.NotNull(projectJson);
            var endpointSnapshot = projectJson.RootElement.GetProperty("statusSnapshots").EnumerateArray()
                .Single(snapshot => snapshot.GetProperty("providerId").GetString() == "http-endpoint");
            Assert.Contains(endpointSnapshot.GetProperty("signals").EnumerateArray(), signal =>
                signal.GetProperty("id").GetString() == "http-endpoint-loopback-health"
                && signal.GetProperty("level").GetString() == "Healthy");
            var executeResponse = await client.PostAsJsonAsync("/api/operations/execute", new
            {
                projectId = "placeholder",
                operationId = "http-endpoint-check",
                input = new { endpointId = "loopback-health" },
                requestedBy = "e2e-test"
            });
            Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);
            var executionJson = await executeResponse.Content.ReadFromJsonAsync<JsonDocument>();
            Assert.NotNull(executionJson);
            Assert.Equal("http-endpoint-check", executionJson.RootElement.GetProperty("operationId").GetString());
            var result = executionJson.RootElement.GetProperty("result");
            Assert.Equal("Succeeded", result.GetProperty("status").GetString());
            Assert.Equal("loopback-health", result.GetProperty("output").GetProperty("endpointId").GetString());
            var historyResponse = await client.GetAsync("/api/operations/history?take=5");
            Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
            var historyText = await historyResponse.Content.ReadAsStringAsync();
            Assert.Contains("\"http-endpoint-check\"", historyText);
            Assert.DoesNotContain(server.Url, historyText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("?probe=r2", historyText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__ControlPlane", previousConnectionString);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
            Environment.SetEnvironmentVariable("HttpEndpoint__ConfigPath", previousConfigPath);
            foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
            {
                var path = tempDbPath + suffix;
                if (File.Exists(path)) File.Delete(path);
            }
            if (File.Exists(configPath)) File.Delete(configPath);
        }
    }
}
