using System.Net;
using System.Text;
using ControlPlane.Api.Tests.Support;
using ControlPlane.Core.Concepts;
using ControlPlane.ExternalPlugins;

namespace ControlPlane.Api.Tests;

public sealed class ExternalRemoteStatusProviderTests
{
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
