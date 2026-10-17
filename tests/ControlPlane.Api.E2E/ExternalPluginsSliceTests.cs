using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

namespace ControlPlane.Api.E2E;

/// <summary>
/// End-to-end observe, confirm, and act slice for external HTTP plugins. A
/// fabricated plugin runs as a separate Kestrel process; no live external system
/// is contacted.
/// </summary>
public sealed class ExternalPluginsSliceTests
{
    [Fact]
    public async Task ExternalPluginFlow_is_exposed_and_executable()
    {
        await using var fixture = await FixtureProcess.StartAsync("normal");
        await using var harness = new ExternalPluginTestHarness();
        harness.SetPlugin(0, "sample", fixture.BaseUrl);
        using var client = harness.CreateClient();

        var snapshots = await GetSnapshotsAsync(client);
        Assert.Contains("sample.health", snapshots.Select(snapshot => snapshot.GetProperty("providerId").GetString()));
        Assert.Contains("sample.bridge", snapshots.Select(snapshot => snapshot.GetProperty("providerId").GetString()));

        var operations = await GetOperationsAsync(client);
        var restart = operations.Single(operation => operation.GetProperty("id").GetString() == "sample.restart");
        Assert.True(restart.GetProperty("requiresConfirmation").GetBoolean());

        var executeResponse = await client.PostAsJsonAsync("/api/operations/execute", new
        {
            projectId = "placeholder",
            operationId = "sample.ping",
            input = new Dictionary<string, string?>(),
            requestedBy = "e2e-test"
        });
        Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);
        var executionJson = await executeResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(executionJson);
        var result = executionJson.RootElement.GetProperty("result");
        Assert.Equal("Succeeded", result.GetProperty("status").GetString());
        Assert.Equal("pong", result.GetProperty("output").GetProperty("message").GetString());

        var historyResponse = await client.GetAsync("/api/operations/history?take=5");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var historyText = await historyResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"sample.ping\"", historyText);
    }

    [Fact]
    public async Task UnreachablePlugin_starts_with_a_warning_and_no_operations()
    {
        var port = GetClosedPort();
        await using var harness = new ExternalPluginTestHarness();
        harness.SetPlugin(0, "sample", $"http://127.0.0.1:{port}");
        using var client = harness.CreateClient();

        var bridge = await GetSnapshotAsync(client, "sample.bridge");
        Assert.Equal("Warning", bridge.GetProperty("overallLevel").GetString());
        Assert.Contains(bridge.GetProperty("signals").EnumerateArray(), signal =>
            signal.GetProperty("id").GetString() == "unreachable");

        var operations = await GetOperationsAsync(client);
        Assert.DoesNotContain(operations, operation =>
            operation.GetProperty("id").GetString()?.StartsWith("sample.", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(await GetSnapshotsAsync(client), snapshot =>
            snapshot.GetProperty("providerId").GetString() == "sample.health");
    }

    [Fact]
    public async Task InvalidCapabilities_start_with_a_warning()
    {
        await using var fixture = await FixtureProcess.StartAsync("invalid-capabilities");
        await using var harness = new ExternalPluginTestHarness();
        harness.SetPlugin(0, "sample", fixture.BaseUrl);
        using var client = harness.CreateClient();

        var bridge = await GetSnapshotAsync(client, "sample.bridge");
        Assert.Equal("Warning", bridge.GetProperty("overallLevel").GetString());
        Assert.Contains(bridge.GetProperty("signals").EnumerateArray(), signal =>
            signal.GetProperty("id").GetString() == "invalid-capabilities");
    }

    [Fact]
    public async Task DuplicatePluginName_fails_startup()
    {
        await using var harness = new ExternalPluginTestHarness();
        harness.SetPlugin(0, "sample", "http://127.0.0.1:1");
        harness.SetPlugin(1, "sample", "http://127.0.0.1:2");

        var exception = Assert.ThrowsAny<Exception>(() => harness.CreateClient());
        Assert.Contains("duplicate", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExcludedRemoteId_registers_the_remaining_capabilities_with_a_warning()
    {
        await using var fixture = await FixtureProcess.StartAsync("excluded-id");
        await using var harness = new ExternalPluginTestHarness();
        harness.SetPlugin(0, "sample", fixture.BaseUrl);
        using var client = harness.CreateClient();

        var bridge = await GetSnapshotAsync(client, "sample.bridge");
        Assert.Equal("Warning", bridge.GetProperty("overallLevel").GetString());

        var providerIds = (await GetSnapshotsAsync(client))
            .Select(snapshot => snapshot.GetProperty("providerId").GetString());
        Assert.Contains("sample.health", providerIds);

        var operationIds = (await GetOperationsAsync(client))
            .Select(operation => operation.GetProperty("id").GetString());
        Assert.Contains("sample.ping", operationIds);
        Assert.DoesNotContain("sample.bad id!", operationIds);
    }

    [Fact]
    public async Task ExecuteTimeout_is_recorded_with_the_bridge_timeout_code()
    {
        await using var fixture = await FixtureProcess.StartAsync("slow-execute");
        await using var harness = new ExternalPluginTestHarness();
        harness.SetPlugin(0, "sample", fixture.BaseUrl);
        using var client = harness.CreateClient();

        var executeResponse = await client.PostAsJsonAsync("/api/operations/execute", new
        {
            projectId = "placeholder",
            operationId = "sample.ping",
            input = new Dictionary<string, string?>(),
            requestedBy = "e2e-test"
        });
        Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);
        var executionJson = await executeResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var result = executionJson!.RootElement.GetProperty("result");
        Assert.Equal("Failed", result.GetProperty("status").GetString());
        Assert.Equal("external_operation_timeout", result.GetProperty("errorCode").GetString());

        var historyText = await (await client.GetAsync("/api/operations/history?take=5")).Content.ReadAsStringAsync();
        Assert.Contains("external_operation_timeout", historyText);
    }

    [Fact]
    public async Task ValidateFailure_rejects_without_executing()
    {
        await using var fixture = await FixtureProcess.StartAsync("invalid-validate");
        await using var harness = new ExternalPluginTestHarness();
        harness.SetPlugin(0, "sample", fixture.BaseUrl);
        using var client = harness.CreateClient();

        var executeResponse = await client.PostAsJsonAsync("/api/operations/execute", new
        {
            projectId = "placeholder",
            operationId = "sample.ping",
            input = new Dictionary<string, string?>(),
            requestedBy = "e2e-test"
        });
        Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);
        var executionJson = await executeResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var result = executionJson!.RootElement.GetProperty("result");
        Assert.Equal("Rejected", result.GetProperty("status").GetString());
        Assert.Equal("operation_validation_failed", result.GetProperty("errorCode").GetString());
        Assert.Contains("external_operation_unexpected_status", result.GetProperty("message").GetString());
    }

    private static async Task<JsonElement[]> GetSnapshotsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/projects/placeholder");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return json!.RootElement.GetProperty("statusSnapshots").EnumerateArray().ToArray();
    }

    private static async Task<JsonElement> GetSnapshotAsync(HttpClient client, string providerId)
    {
        var snapshots = await GetSnapshotsAsync(client);
        return snapshots.Single(snapshot => snapshot.GetProperty("providerId").GetString() == providerId);
    }

    private static async Task<JsonElement[]> GetOperationsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/projects/placeholder");
        var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return json!.RootElement.GetProperty("operations").EnumerateArray().ToArray();
    }

    private static int GetClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
