using System.Net;
using System.Text;
using ControlPlane.Api.Tests.Support;
using ControlPlane.Core.Concepts;
using ControlPlane.ExternalPlugins;

namespace ControlPlane.Api.Tests;

public sealed class ExternalRemoteStatusProviderTests
{
    [Theory]
    [InlineData("missing-signals")]
    [InlineData("null-signals")]
    [InlineData("null-signal")]
    [InlineData("missing-title")]
    [InlineData("numeric-overall-level")]
    [InlineData("numeric-signal-level")]
    public async Task Malformed_status_snapshot_returns_a_warning_without_throwing(string kind)
    {
        var body = kind switch
        {
            "missing-signals" => """{ "projectId": "p", "providerId": "remote", "overallLevel": "Healthy", "observedAtUtc": "2020-01-01T00:00:00+00:00" }""",
            "null-signals" => """{ "projectId": "p", "providerId": "remote", "overallLevel": "Healthy", "observedAtUtc": "2020-01-01T00:00:00+00:00", "signals": null }""",
            "null-signal" => """{ "projectId": "p", "providerId": "remote", "overallLevel": "Healthy", "observedAtUtc": "2020-01-01T00:00:00+00:00", "signals": [null] }""",
            "missing-title" => """{ "projectId": "p", "providerId": "remote", "overallLevel": "Healthy", "observedAtUtc": "2020-01-01T00:00:00+00:00", "signals": [{ "id": "s", "description": "d", "level": "Healthy", "observedAtUtc": "2020-01-01T00:00:00+00:00" }] }""",
            "numeric-overall-level" => """{ "projectId": "p", "providerId": "remote", "overallLevel": 7, "observedAtUtc": "2020-01-01T00:00:00+00:00", "signals": [] }""",
            _ => """{ "projectId": "p", "providerId": "remote", "overallLevel": "Healthy", "observedAtUtc": "2020-01-01T00:00:00+00:00", "signals": [{ "id": "s", "title": "t", "description": "d", "level": 7, "observedAtUtc": "2020-01-01T00:00:00+00:00" }] }"""
        };
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json(body)));

        var snapshot = await CreateProvider(handler).GetStatusAsync(Project("requested"), CancellationToken.None);

        Assert.Equal(StatusLevel.Warning, snapshot.OverallLevel);
        Assert.Equal("sample.health", snapshot.ProviderId);
        Assert.Equal("requested", snapshot.ProjectId);
        Assert.Contains(snapshot.Signals, signal => signal.Id == "status-call-failed");
    }

    [Fact]
    public async Task Status_passes_through_and_overwrites_the_identity()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json("""
            {
              "projectId": "remote-project",
              "providerId": "remote",
              "overallLevel": "Healthy",
              "observedAtUtc": "2020-01-01T00:00:00+00:00",
              "signals": []
            }
            """)));

        var snapshot = await CreateProvider(handler).GetStatusAsync(Project("requested"), CancellationToken.None);

        Assert.Equal("sample.health", snapshot.ProviderId);
        Assert.Equal("requested", snapshot.ProjectId);
        Assert.Equal(StatusLevel.Healthy, snapshot.OverallLevel);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("invalid-json")]
    [InlineData("unknown-level")]
    public async Task Failed_status_call_returns_a_warning_without_throwing(string kind)
    {
        var handler = kind switch
        {
            "status" => new StubHttpMessageHandler((_, _) => Task.FromResult(Json("{}", HttpStatusCode.BadGateway))),
            "invalid-json" => new StubHttpMessageHandler((_, _) => Task.FromResult(Json("not json"))),
            _ => new StubHttpMessageHandler((_, _) => Task.FromResult(Json("""
                { "projectId": "p", "providerId": "remote", "overallLevel": "Bogus", "observedAtUtc": "2020-01-01T00:00:00+00:00", "signals": [] }
                """)))
        };

        var snapshot = await CreateProvider(handler).GetStatusAsync(Project("requested"), CancellationToken.None);

        Assert.Equal(StatusLevel.Warning, snapshot.OverallLevel);
        Assert.Equal("sample.health", snapshot.ProviderId);
        Assert.Equal("requested", snapshot.ProjectId);
        Assert.Contains(snapshot.Signals, signal => signal.Id == "status-call-failed");
    }

    [Fact]
    public async Task Status_with_an_invalid_response_charset_returns_a_warning_without_throwing()
    {
        using var server = new LoopbackHttpServer((context, _) =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json; charset=bogus";
            var bytes = Encoding.UTF8.GetBytes("not json");
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes);
            context.Response.Close();
            return Task.CompletedTask;
        });

        var provider = new ExternalRemoteStatusProvider(
            "sample",
            "health",
            server.BaseAddress,
            new RealHttpClientFactory(),
            TimeSpan.FromSeconds(10));

        var snapshot = await provider.GetStatusAsync(Project("requested"), CancellationToken.None);

        Assert.Equal(StatusLevel.Warning, snapshot.OverallLevel);
        Assert.Equal("sample.health", snapshot.ProviderId);
        Assert.Contains(snapshot.Signals, signal => signal.Id == "status-call-failed");
    }

    [Fact]
    public async Task Transport_failure_signal_does_not_leak_the_exception_message()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("down at host:5099"));

        var snapshot = await CreateProvider(handler).GetStatusAsync(Project("requested"), CancellationToken.None);

        var signal = Assert.Single(snapshot.Signals);
        Assert.Equal("status-call-failed", signal.Id);
        Assert.DoesNotContain("host:5099", signal.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Caller_cancellation_propagates()
    {
        var handler = new StubHttpMessageHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Json("{}");
        });
        using var cancellation = new CancellationTokenSource();
        var task = CreateProvider(handler, TimeSpan.FromSeconds(30))
            .GetStatusAsync(Project("requested"), cancellation.Token);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    private static ExternalRemoteStatusProvider CreateProvider(StubHttpMessageHandler handler, TimeSpan? timeout = null)
        => new(
            "sample",
            "health",
            new Uri("http://localhost:5099"),
            new StubHttpClientFactory(handler),
            timeout ?? TimeSpan.FromSeconds(10));

    private static Project Project(string id) => new(id, "Project", [], []);

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
}
