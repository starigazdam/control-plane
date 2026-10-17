using ControlPlane.Api.Tests.Support;
using ControlPlane.Core.Concepts;
using ControlPlane.ExternalPlugins;

namespace ControlPlane.Api.Tests;

public sealed class ExternalBridgeStatusProviderTests
{
    [Fact]
    public async Task Discovery_failure_logs_one_warning_with_the_plugin_name_and_reason()
    {
        var capture = new CapturingLoggerProvider();
        var provider = new ExternalBridgeStatusProvider(
            Discovery(ExternalDiscoveryStatus.Unreachable, baseUri: "http://plugin.internal:5099/secret"),
            capture.CreateLogger("test"));

        await provider.GetStatusAsync(Project("p"), CancellationToken.None);
        await provider.GetStatusAsync(Project("p"), CancellationToken.None);

        var message = Assert.Single(capture.Messages);
        Assert.Contains("sample", message, StringComparison.Ordinal);
        Assert.Contains("unreachable", message, StringComparison.Ordinal);
        Assert.DoesNotContain("plugin.internal", message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_capabilities_failure_logs_the_invalid_capabilities_reason()
    {
        var capture = new CapturingLoggerProvider();
        var provider = new ExternalBridgeStatusProvider(
            Discovery(ExternalDiscoveryStatus.InvalidCapabilities),
            capture.CreateLogger("test"));

        await provider.GetStatusAsync(Project("p"), CancellationToken.None);

        Assert.Contains("invalid-capabilities", Assert.Single(capture.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Successful_discovery_logs_nothing()
    {
        var capture = new CapturingLoggerProvider();
        var provider = new ExternalBridgeStatusProvider(
            Discovery(ExternalDiscoveryStatus.Succeeded),
            capture.CreateLogger("test"));

        await provider.GetStatusAsync(Project("p"), CancellationToken.None);

        Assert.Empty(capture.Messages);
    }

    [Fact]
    public async Task Exclusion_signal_ids_are_unique()
    {
        var provider = new ExternalBridgeStatusProvider(Discovery(
            ExternalDiscoveryStatus.Succeeded,
            exclusions:
            [
                new ExternalCapabilityExclusion("operation", "ping", "first"),
                new ExternalCapabilityExclusion("operation", "ping", "second")
            ]));

        var snapshot = await provider.GetStatusAsync(Project("p"), CancellationToken.None);

        var ids = snapshot.Signals.Select(signal => signal.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    private static ExternalPluginDiscovery Discovery(
        ExternalDiscoveryStatus status,
        string name = "sample",
        string baseUri = "http://localhost:5099",
        IReadOnlyList<ExternalCapabilityExclusion>? exclusions = null)
        => new(name, new Uri(baseUri), status, [], [], exclusions ?? [], DateTimeOffset.UtcNow);

    private static Project Project(string id) => new(id, "Project", [], []);
}
