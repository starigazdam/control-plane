using System.Net;
using ControlPlane.Api.Tests.Support;
using ControlPlane.HttpEndpoint;
namespace ControlPlane.Api.Tests;
public sealed class HttpEndpointScanCoordinatorTests
{
    [Fact]
    public async Task Concurrent_callers_share_one_scan()
    {
        var handler = SlowHandler(TimeSpan.FromMilliseconds(200));
        var configuration = Configuration("a", "b", "c");
        var coordinator = CreateCoordinator(handler);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => coordinator.GetScanAsync(configuration, CancellationToken.None)));
        Assert.All(results, result => Assert.Equal(3, result.Outcomes.Count));
        Assert.Equal(3, handler.RequestCount);
    }
    [Fact]
    public async Task Results_are_cached_within_the_cache_window()
    {
        var handler = OkHandler();
        var configuration = Configuration("a", "b");
        var coordinator = CreateCoordinator(handler, TimeSpan.FromSeconds(30));
        await coordinator.GetScanAsync(configuration, CancellationToken.None);
        await coordinator.GetScanAsync(configuration, CancellationToken.None);
        await coordinator.GetScanAsync(configuration, CancellationToken.None);
        Assert.Equal(2, handler.RequestCount);
    }
    [Fact]
    public async Task Cache_expiry_starts_a_new_scan()
    {
        var handler = OkHandler();
        var configuration = Configuration("a");
        var coordinator = CreateCoordinator(handler, TimeSpan.FromMilliseconds(1));
        await coordinator.GetScanAsync(configuration, CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        await coordinator.GetScanAsync(configuration, CancellationToken.None);
        Assert.Equal(2, handler.RequestCount);
    }
    [Fact]
    public async Task Caller_cancellation_does_not_cancel_the_shared_scan()
    {
        var handler = SlowHandler(TimeSpan.FromMilliseconds(300));
        var configuration = Configuration("a", "b");
        var coordinator = CreateCoordinator(handler, TimeSpan.FromSeconds(30));
        var sharedScan = coordinator.GetScanAsync(configuration, CancellationToken.None);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.GetScanAsync(configuration, cancelled.Token));
        var result = await sharedScan;
        Assert.Equal(2, result.Outcomes.Count);
        Assert.Equal(2, handler.RequestCount);
    }
    [Fact]
    public async Task Overall_budget_maps_unfinished_checks_to_timeout()
    {
        var handler = SlowHandler(TimeSpan.FromSeconds(30));
        var configuration = Configuration("a", "b", "c");
        var coordinator = CreateCoordinator(handler, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(200));
        var result = await coordinator.GetScanAsync(configuration, CancellationToken.None);
        Assert.Equal(3, result.Outcomes.Count);
        Assert.All(result.Outcomes, outcome => Assert.Equal(EndpointCheckOutcome.Timeout, outcome.ErrorCode));
    }
    private static StubHttpMessageHandler OkHandler()
        => new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
    private static StubHttpMessageHandler SlowHandler(TimeSpan delay)
        => new(async (_, token) =>
        {
            await Task.Delay(delay, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
    private static HttpEndpointScanCoordinator CreateCoordinator(StubHttpMessageHandler handler, TimeSpan? cacheDuration = null, TimeSpan? scanBudget = null)
        => new(new HttpEndpointChecker(new StubHttpClientFactory(handler)), new FakeHostApplicationLifetime())
        {
            CacheDuration = cacheDuration ?? TimeSpan.FromSeconds(30),
            ScanBudget = scanBudget ?? TimeSpan.FromSeconds(5)
        };
    private static HttpEndpointConfiguration Configuration(params string[] ids)
        => new(
            isEnabled: true,
            endpoints: ids.Select(id => new HttpEndpointDefinition(id, $"Endpoint {id}", "http://localhost/health", 200, "GET", TimeSpan.FromSeconds(5))).ToArray(),
            warnings: [],
            hasScheduleWarning: false);
}
