using System.Diagnostics;
using ControlPlane.Api.Tests.Support;
using ControlPlane.HttpEndpoint;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;
namespace ControlPlane.Api.Tests;
[Collection(ProcessGlobalObserverCollection.Name)]
public sealed class HttpEndpointPrivacyTests
{
    private const string CheckUrl = "health?secretQuery=do-not-log";
    [Fact]
    public async Task Named_client_logs_never_contain_the_configured_url()
    {
        var capture = new CapturingLoggerProvider();
        using var server = new LoopbackHttpServer((c, _) => LoopbackHttpServer.WriteStatusAsync(c, 200));
        using var provider = TestServiceProvider.Create(logging => logging.AddProvider(capture));
        var checker = new HttpEndpointChecker(provider.GetRequiredService<IHttpClientFactory>());
        var url = server.Url(CheckUrl);
        await checker.CheckAsync(new HttpEndpointDefinition("alpha", "Alpha", url, 200, "GET", TimeSpan.FromSeconds(5)), CancellationToken.None);
        var logs = string.Join("\n", capture.Messages);
        Assert.DoesNotContain(url, logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secretQuery", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-log", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("/health", logs, StringComparison.Ordinal);
    }
    [Fact]
    public async Task Named_client_activities_never_contain_the_configured_url()
    {
        var exported = new List<Activity>();
        using var checkServer = new LoopbackHttpServer((c, _) => LoopbackHttpServer.WriteStatusAsync(c, 200));
        using var controlServer = new LoopbackHttpServer((c, _) => LoopbackHttpServer.WriteStatusAsync(c, 200));
        using var provider = TestServiceProvider.Create();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddHttpClientInstrumentation(options =>
                options.FilterHttpRequestMessage = Microsoft.Extensions.Hosting.Extensions.ShouldCollectHttpClientTrace)
            .AddProcessor(new CollectingActivityProcessor(exported))
            .Build();
        var checker = new HttpEndpointChecker(provider.GetRequiredService<IHttpClientFactory>());
        var checkUrl = checkServer.Url("health?secretQuery=do-not-trace");
        // A control request without the marker proves the pipeline captures activities at all.
        using (var controlClient = new HttpClient())
        using (await controlClient.GetAsync(controlServer.Url("control?controlQuery=visible"))) { }
        await checker.CheckAsync(new HttpEndpointDefinition("alpha", "Alpha", checkUrl, 200, "GET", TimeSpan.FromSeconds(5)), CancellationToken.None);
        tracerProvider.ForceFlush();
        var recorded = string.Join("\n", exported.Select(activity => string.Join(" ", activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))));
        Assert.Contains($"localhost:{controlServer.BaseAddress.Port}", recorded, StringComparison.Ordinal);
        Assert.Contains("/control", recorded, StringComparison.Ordinal);
        Assert.DoesNotContain($"localhost:{checkServer.BaseAddress.Port}", recorded, StringComparison.Ordinal);
        Assert.DoesNotContain("/health", recorded, StringComparison.Ordinal);
        Assert.DoesNotContain("secretQuery", recorded, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-trace", recorded, StringComparison.Ordinal);
        Assert.DoesNotContain(checkUrl, recorded, StringComparison.OrdinalIgnoreCase);
    }
    private sealed class CollectingActivityProcessor : BaseProcessor<Activity>
    {
        private readonly List<Activity> _activities;
        public CollectingActivityProcessor(List<Activity> activities) => _activities = activities;
        public override void OnEnd(Activity data)
        {
            lock (_activities) _activities.Add(data);
        }
    }
}
