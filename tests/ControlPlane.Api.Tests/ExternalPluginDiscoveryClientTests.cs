using System.Diagnostics;
using ControlPlane.Api.Tests.Support;
using ControlPlane.ExternalPlugins;

namespace ControlPlane.Api.Tests;

public sealed class ExternalPluginDiscoveryClientTests
{
    [Fact]
    public void Stalled_capabilities_returns_unreachable_within_the_injected_timeout()
    {
        using var server = new LoopbackHttpServer(async (context, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            await LoopbackHttpServer.WriteStatusAsync(context, 200);
        });
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        var stopwatch = Stopwatch.StartNew();
        var discovery = ExternalPluginDiscoveryClient.Discover(
            client,
            Definition(server.BaseAddress),
            TimeSpan.FromMilliseconds(200));
        stopwatch.Stop();

        Assert.Equal(ExternalDiscoveryStatus.Unreachable, discovery.Status);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Elapsed {stopwatch.Elapsed}.");
    }

    [Fact]
    public void Non_success_capabilities_returns_invalid_capabilities()
    {
        using var server = new LoopbackHttpServer((context, _) => LoopbackHttpServer.WriteStatusAsync(context, 500));
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        var discovery = ExternalPluginDiscoveryClient.Discover(
            client,
            Definition(server.BaseAddress),
            TimeSpan.FromSeconds(5));

        Assert.Equal(ExternalDiscoveryStatus.InvalidCapabilities, discovery.Status);
    }

    private static ExternalPluginDefinition Definition(Uri baseUri) => new("sample", baseUri);
}
