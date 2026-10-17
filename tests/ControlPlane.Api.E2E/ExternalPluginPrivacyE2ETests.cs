using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ControlPlane.Api.E2E;

/// <summary>
/// Privacy and no-retry through the real host pipeline: ServiceDefaults and its
/// OpenTelemetry HTTP client instrumentation are active while an ambient
/// <see cref="Activity"/> exists. The fixture records the path and header names of
/// every request it receives and exposes them at a test-only endpoint.
/// </summary>
public sealed class ExternalPluginPrivacyE2ETests
{
    private static readonly string[] ForbiddenHeaders =
    [
        "traceparent",
        "tracestate",
        "baggage",
        "Request-Id",
        "Correlation-Context",
        "Authorization",
        "Cookie"
    ];

    [Fact]
    public async Task Bridge_requests_carry_no_trace_or_identity_headers()
    {
        await using var fixture = await FixtureProcess.StartAsync("normal");
        await using var harness = new ExternalPluginTestHarness();
        harness.SetPlugin(0, "sample", fixture.BaseUrl);
        using var client = harness.CreateClient();
        using var fixtureClient = new HttpClient { BaseAddress = new Uri(fixture.BaseUrl) };
        await ResetAsync(fixtureClient);

        using var activity = new Activity("e2e-privacy").SetIdFormat(ActivityIdFormat.W3C).Start();

        var overview = await client.GetAsync("/api/projects/placeholder");
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);

        var execute = await client.PostAsJsonAsync("/api/operations/execute", new
        {
            projectId = "placeholder",
            operationId = "sample.ping",
            input = new Dictionary<string, string?>(),
            requestedBy = "e2e-test"
        });
        Assert.Equal(HttpStatusCode.OK, execute.StatusCode);

        var requests = await GetRequestsAsync(fixtureClient);
        Assert.Contains(requests, request => request.Path == "/status/health");
        Assert.Contains(requests, request => request.Path == "/operations/ping/validate");
        Assert.Contains(requests, request => request.Path == "/operations/ping/execute");

        foreach (var request in requests)
        {
            var headers = request.Headers.Select(header => header.ToLowerInvariant()).ToHashSet();
            foreach (var forbidden in ForbiddenHeaders)
            {
                Assert.DoesNotContain(forbidden.ToLowerInvariant(), headers);
            }
        }
    }

    [Fact]
    public async Task Slow_execute_timeout_sends_exactly_one_execute_request()
    {
        await using var fixture = await FixtureProcess.StartAsync("slow-execute");
        await using var harness = new ExternalPluginTestHarness();
        harness.SetPlugin(0, "sample", fixture.BaseUrl);
        using var client = harness.CreateClient();
        using var fixtureClient = new HttpClient { BaseAddress = new Uri(fixture.BaseUrl) };
        await ResetAsync(fixtureClient);

        using var activity = new Activity("e2e-timeout").SetIdFormat(ActivityIdFormat.W3C).Start();

        var execute = await client.PostAsJsonAsync("/api/operations/execute", new
        {
            projectId = "placeholder",
            operationId = "sample.ping",
            input = new Dictionary<string, string?>(),
            requestedBy = "e2e-test"
        });
        Assert.Equal(HttpStatusCode.OK, execute.StatusCode);

        var requests = await GetRequestsAsync(fixtureClient);
        Assert.Equal(1, requests.Count(request => request.Path == "/operations/ping/execute"));
    }

    [Fact]
    public async Task Invalid_validate_sends_exactly_one_validate_request_and_no_execute()
    {
        await using var fixture = await FixtureProcess.StartAsync("invalid-validate");
        await using var harness = new ExternalPluginTestHarness();
        harness.SetPlugin(0, "sample", fixture.BaseUrl);
        using var client = harness.CreateClient();
        using var fixtureClient = new HttpClient { BaseAddress = new Uri(fixture.BaseUrl) };
        await ResetAsync(fixtureClient);

        using var activity = new Activity("e2e-validate").SetIdFormat(ActivityIdFormat.W3C).Start();

        var execute = await client.PostAsJsonAsync("/api/operations/execute", new
        {
            projectId = "placeholder",
            operationId = "sample.ping",
            input = new Dictionary<string, string?>(),
            requestedBy = "e2e-test"
        });
        Assert.Equal(HttpStatusCode.OK, execute.StatusCode);

        var requests = await GetRequestsAsync(fixtureClient);
        Assert.Equal(1, requests.Count(request => request.Path == "/operations/ping/validate"));
        Assert.Equal(0, requests.Count(request => request.Path == "/operations/ping/execute"));
    }

    private static async Task ResetAsync(HttpClient fixtureClient)
    {
        using var response = await fixtureClient.PostAsync("/__requests/reset", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<RecordedRequest[]> GetRequestsAsync(HttpClient fixtureClient)
    {
        using var response = await fixtureClient.GetAsync("/__requests");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(json);
        return json!.RootElement.EnumerateArray().Select(element => new RecordedRequest(
            element.GetProperty("method").GetString() ?? string.Empty,
            element.GetProperty("path").GetString() ?? string.Empty,
            element.GetProperty("headers").EnumerateArray().Select(header => header.GetString() ?? string.Empty).ToArray()))
            .ToArray();
    }

    private sealed record RecordedRequest(string Method, string Path, IReadOnlyList<string> Headers);
}
