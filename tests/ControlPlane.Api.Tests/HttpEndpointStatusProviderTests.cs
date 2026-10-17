using System.Net;
using ControlPlane.Api.Tests.Support;
using ControlPlane.Core.Concepts;
using ControlPlane.HttpEndpoint;
using ControlPlane.HttpEndpoint.StatusProviders;
namespace ControlPlane.Api.Tests;
public sealed class HttpEndpointStatusProviderTests
{
    private static readonly Project Project = new("placeholder", "Placeholder", [], []);
    [Fact]
    public async Task Disabled_plugin_returns_unknown_with_no_signals()
    {
        var snapshot = await CreateProvider(HttpEndpointConfiguration.Disabled, Ok()).GetStatusAsync(Project, CancellationToken.None);
        Assert.Equal(StatusLevel.Unknown, snapshot.OverallLevel);
        Assert.Empty(snapshot.Signals);
        Assert.Equal("http-endpoint", snapshot.ProviderId);
    }
    [Fact]
    public async Task Enabled_without_endpoints_is_unknown_with_no_signals()
    {
        var snapshot = await CreateProvider(ConfigurationWith(), Ok()).GetStatusAsync(Project, CancellationToken.None);
        Assert.Equal(StatusLevel.Unknown, snapshot.OverallLevel);
        Assert.Empty(snapshot.Signals);
    }
    [Theory]
    [InlineData("expected", StatusLevel.Healthy)]
    [InlineData("unexpected", StatusLevel.Warning)]
    [InlineData("timeout", StatusLevel.Critical)]
    [InlineData("transport", StatusLevel.Critical)]
    public async Task Outcome_maps_to_status_level(string kind, StatusLevel expectedLevel)
    {
        var timeout = kind == "timeout" ? TimeSpan.FromMilliseconds(50) : (TimeSpan?)null;
        var configuration = ConfigurationWith(Definition("alpha", 200, timeout: timeout));
        var snapshot = await CreateProvider(configuration, HandlerFor(kind)).GetStatusAsync(Project, CancellationToken.None);
        var signal = Assert.Single(snapshot.Signals);
        Assert.Equal("http-endpoint-alpha", signal.Id);
        Assert.Equal(expectedLevel, signal.Level);
        Assert.Equal(expectedLevel, snapshot.OverallLevel);
    }
    [Fact]
    public async Task Invalid_entries_produce_warning_signals_identified_by_index()
    {
        var configuration = new HttpEndpointConfiguration(
            isEnabled: true,
            endpoints: [],
            warnings: [new HttpEndpointConfigWarning(0, "invalid_url"), new HttpEndpointConfigWarning(2, "invalid_method")],
            hasScheduleWarning: false);
        var snapshot = await CreateProvider(configuration, Ok()).GetStatusAsync(Project, CancellationToken.None);
        Assert.Equal(2, snapshot.Signals.Count);
        Assert.All(snapshot.Signals, signal => Assert.Equal(StatusLevel.Warning, signal.Level));
        Assert.Contains(snapshot.Signals, signal => signal.Id == "http-endpoint-config-0");
        Assert.Contains(snapshot.Signals, signal => signal.Id == "http-endpoint-config-2");
        Assert.Equal(StatusLevel.Warning, snapshot.OverallLevel);
    }
    [Fact]
    public async Task Schedule_fields_produce_exactly_one_plugin_level_warning()
    {
        var configuration = new HttpEndpointConfiguration(isEnabled: true, endpoints: [Definition("alpha", 200)], warnings: [], hasScheduleWarning: true);
        var snapshot = await CreateProvider(configuration, Ok()).GetStatusAsync(Project, CancellationToken.None);
        Assert.Single(snapshot.Signals, signal => signal.Id == "http-endpoint-schedule-ignored");
        Assert.Equal(2, snapshot.Signals.Count);
    }
    [Fact]
    public async Task Overall_level_is_the_worst_signal()
    {
        var configuration = ConfigurationWith(Definition("alpha", 200), Definition("beta", 200));
        var callCount = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            var status = Interlocked.Increment(ref callCount) == 1 ? HttpStatusCode.OK : HttpStatusCode.InternalServerError;
            return Task.FromResult(new HttpResponseMessage(status));
        });
        var snapshot = await CreateProvider(configuration, handler).GetStatusAsync(Project, CancellationToken.None);
        Assert.Equal(StatusLevel.Warning, snapshot.OverallLevel);
        Assert.Equal(2, snapshot.Signals.Count);
    }
    private static StubHttpMessageHandler HandlerFor(string kind) => kind switch
    {
        "expected" => Ok(),
        "unexpected" => new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))),
        "timeout" => new StubHttpMessageHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }),
        _ => new StubHttpMessageHandler((_, _) => throw new HttpRequestException("transport down"))
    };
    private static StubHttpMessageHandler Ok()
        => new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
    private static HttpEndpointStatusProvider CreateProvider(HttpEndpointConfiguration configuration, StubHttpMessageHandler handler)
    {
        var coordinator = new HttpEndpointScanCoordinator(new HttpEndpointChecker(new StubHttpClientFactory(handler)), new FakeHostApplicationLifetime())
        {
            CacheDuration = TimeSpan.FromMilliseconds(50),
            ScanBudget = TimeSpan.FromSeconds(5)
        };
        return new HttpEndpointStatusProvider(configuration, coordinator);
    }
    private static HttpEndpointConfiguration ConfigurationWith(params HttpEndpointDefinition[] definitions)
        => new(isEnabled: true, endpoints: definitions, warnings: [], hasScheduleWarning: false);
    private static HttpEndpointDefinition Definition(string id, int expectedStatus, TimeSpan? timeout = null)
        => new(id, $"Endpoint {id}", "http://localhost/health", expectedStatus, "GET", timeout ?? TimeSpan.FromSeconds(5));
}
