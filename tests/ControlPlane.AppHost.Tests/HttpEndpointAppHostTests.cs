using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
namespace ControlPlane.AppHost.Tests;
/// <summary>
/// Aspire-backed observe-and-act slice for configured HTTP endpoint checks. It uses a
/// fabricated loopback endpoint and makes no live external requests.
/// </summary>
public sealed class HttpEndpointAppHostTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);
    [Fact(Timeout = 180_000)]
    public async Task HttpEndpointCheckFlow_is_exposed_through_the_app_host()
    {
        using var server = new LoopbackHttpServer();
        var configPath = Path.Combine(Path.GetTempPath(), $"http-endpoint-apphost-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new[]
        {
            new { id = "loopback-health", name = "Loopback health", url = server.Url, expectedStatus = 200, method = "GET", timeoutSeconds = 5 }
        }));
        try
        {
            using var cancellationSource = new CancellationTokenSource(StartupTimeout);
            var cancellationToken = cancellationSource.Token;
            var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.ControlPlane_AppHost>(cancellationToken);
            var apiResource = appHost.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "api");
            appHost.CreateResourceBuilder<IResourceWithEnvironment>(apiResource).WithEnvironment("HttpEndpoint__ConfigPath", configPath);
            await using var app = await appHost.BuildAsync(cancellationToken);
            await app.StartAsync(cancellationToken).WaitAsync(StartupTimeout, cancellationToken);
            await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cancellationToken).WaitAsync(StartupTimeout, cancellationToken);
            var endpoint = app.GetEndpoint("api", "https");
            using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
            using var httpClient = new HttpClient(handler) { BaseAddress = endpoint };
            var projectResponse = await httpClient.GetAsync("/api/projects/placeholder", cancellationToken);
            Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);
            using var projectJson = JsonDocument.Parse(await projectResponse.Content.ReadAsStringAsync(cancellationToken));
            Assert.Contains(
                projectJson.RootElement.GetProperty("statusSnapshots").EnumerateArray(),
                snapshot => snapshot.GetProperty("providerId").GetString() == "http-endpoint");
            var executeResponse = await httpClient.PostAsJsonAsync(
                "/api/operations/execute",
                new { projectId = "placeholder", operationId = "http-endpoint-check", input = new { endpointId = "loopback-health" }, requestedBy = "apphost-test" },
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);
            using var executeJson = JsonDocument.Parse(await executeResponse.Content.ReadAsStringAsync(cancellationToken));
            Assert.Equal("Succeeded", executeJson.RootElement.GetProperty("result").GetProperty("status").GetString());
            var historyResponse = await httpClient.GetAsync("/api/operations/history?take=5", cancellationToken);
            var history = await historyResponse.Content.ReadAsStringAsync(cancellationToken);
            Assert.Contains("http-endpoint-check", history, StringComparison.Ordinal);
            Assert.DoesNotContain(server.Url, history, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(configPath)) File.Delete(configPath);
        }
    }
}
