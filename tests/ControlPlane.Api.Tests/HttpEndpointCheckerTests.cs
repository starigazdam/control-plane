using System.Diagnostics;
using System.Net;
using ControlPlane.Api.Tests.Support;
using ControlPlane.Core.Concepts;
using ControlPlane.HttpEndpoint;
using Microsoft.Extensions.DependencyInjection;
namespace ControlPlane.Api.Tests;
public sealed class HttpEndpointCheckerTests : IDisposable
{
    private readonly ServiceProvider _provider = TestServiceProvider.Create();
    public void Dispose() => _provider.Dispose();
    [Theory]
    [InlineData(200, StatusLevel.Healthy, null, true)]
    [InlineData(503, StatusLevel.Warning, EndpointCheckOutcome.UnexpectedStatus, false)]
    public async Task Status_maps_to_level_and_sends_exactly_one_request(int responseStatus, StatusLevel level, string? errorCode, bool succeeded)
    {
        using var server = new LoopbackHttpServer((c, _) => LoopbackHttpServer.WriteStatusAsync(c, responseStatus));
        var outcome = await Checker().CheckAsync(Definition(server.Url("health?probe=one"), 200), CancellationToken.None);
        Assert.Equal(level, outcome.Level);
        Assert.Equal(succeeded, outcome.Succeeded);
        Assert.Equal(responseStatus, outcome.ObservedStatus);
        Assert.Equal(errorCode, outcome.ErrorCode);
        Assert.Equal(1, server.RequestCount);
    }
    [Fact]
    public async Task Redirect_is_not_followed()
    {
        using var target = new LoopbackHttpServer((c, _) => LoopbackHttpServer.WriteStatusAsync(c, 200));
        using var server = new LoopbackHttpServer((c, _) =>
        {
            c.Response.StatusCode = 302;
            c.Response.RedirectLocation = target.Url("landing");
            c.Response.ContentLength64 = 0;
            c.Response.Close();
            return Task.CompletedTask;
        });
        var outcome = await Checker().CheckAsync(Definition(server.Url("health?probe=redirect"), 200), CancellationToken.None);
        Assert.Equal(302, outcome.ObservedStatus);
        Assert.Equal(StatusLevel.Warning, outcome.Level);
        Assert.Equal(1, server.RequestCount);
        Assert.Equal(0, target.RequestCount);
    }
    [Fact]
    public async Task Get_and_head_methods_are_sent_as_configured()
    {
        var methods = new List<string>();
        using var server = new LoopbackHttpServer(async (c, _) =>
        {
            lock (methods) methods.Add(c.Request.HttpMethod);
            await LoopbackHttpServer.WriteStatusAsync(c, 204);
        });
        await Checker().CheckAsync(Definition(server.Url(), 204, method: "GET"), CancellationToken.None);
        await Checker().CheckAsync(Definition(server.Url("head"), 204, method: "HEAD"), CancellationToken.None);
        Assert.Equal(["GET", "HEAD"], methods);
    }
    [Fact]
    public async Task Timeout_maps_to_timeout_and_honors_the_configured_bound()
    {
        using var server = new LoopbackHttpServer(async (c, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            await LoopbackHttpServer.WriteStatusAsync(c, 200);
        });
        var stopwatch = Stopwatch.StartNew();
        var outcome = await Checker().CheckAsync(
            Definition(server.Url("health?probe=slow"), 200, timeout: TimeSpan.FromSeconds(1)),
            CancellationToken.None);
        stopwatch.Stop();
        Assert.Equal(StatusLevel.Critical, outcome.Level);
        Assert.Equal(EndpointCheckOutcome.Timeout, outcome.ErrorCode);
        Assert.Null(outcome.ObservedStatus);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
    }
    [Fact]
    public async Task Transport_failure_maps_to_request_failed()
    {
        var outcome = await Checker().CheckAsync(
            Definition("http://127.0.0.1:1/health", 200, timeout: TimeSpan.FromSeconds(2)),
            CancellationToken.None);
        Assert.Equal(StatusLevel.Critical, outcome.Level);
        Assert.Equal(EndpointCheckOutcome.RequestFailed, outcome.ErrorCode);
        Assert.Null(outcome.ObservedStatus);
    }
    [Fact]
    public async Task Caller_cancellation_is_observed()
    {
        using var server = new LoopbackHttpServer(async (c, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            await LoopbackHttpServer.WriteStatusAsync(c, 200);
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Checker().CheckAsync(Definition(server.Url(), 200), cancellation.Token));
    }
    [Fact]
    public async Task Response_body_is_not_read()
    {
        using var server = new LoopbackHttpServer(async (c, token) =>
        {
            c.Response.StatusCode = 200;
            c.Response.ContentLength64 = 4096;
            await c.Response.OutputStream.WriteAsync(new byte[16], token);
            await c.Response.OutputStream.FlushAsync(token);
            await Task.Delay(TimeSpan.FromSeconds(30), token);
        });
        var stopwatch = Stopwatch.StartNew();
        var outcome = await Checker().CheckAsync(
            Definition(server.Url("health?probe=body"), 200, timeout: TimeSpan.FromSeconds(10)),
            CancellationToken.None);
        stopwatch.Stop();
        Assert.Equal(StatusLevel.Healthy, outcome.Level);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Elapsed {stopwatch.Elapsed}.");
    }
    [Fact]
    public void Named_client_disables_resilience_discovery_logging_redirects_and_default_timeout()
    {
        using var handler = _provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(HttpEndpointChecker.HttpClientName);
        var types = EnumerateChain(handler).Select(candidate => candidate.GetType().FullName).ToArray();
        Assert.DoesNotContain(types, name => name?.Contains("ResilienceHandler", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(types, name => name?.Contains("ServiceDiscovery", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(types, name => name?.Contains("Logging", StringComparison.Ordinal) == true);
        Assert.False(EnumerateChain(handler).OfType<SocketsHttpHandler>().Single().AllowAutoRedirect);
        Assert.Equal(Timeout.InfiniteTimeSpan, _provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpEndpointChecker.HttpClientName).Timeout);
    }
    private HttpEndpointChecker Checker() => new(_provider.GetRequiredService<IHttpClientFactory>());
    private static IEnumerable<HttpMessageHandler> EnumerateChain(HttpMessageHandler handler)
    {
        HttpMessageHandler? current = handler;
        while (current is not null)
        {
            yield return current;
            current = (current as DelegatingHandler)?.InnerHandler;
        }
    }
    private static HttpEndpointDefinition Definition(string url, int expectedStatus, string method = "GET", TimeSpan? timeout = null)
        => new("endpoint-one", "Endpoint One", url, expectedStatus, method, timeout ?? TimeSpan.FromSeconds(5));
}
