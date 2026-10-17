namespace ControlPlane.ExternalPlugins;

internal enum ExternalDiscoveryStatus
{
    Succeeded,
    Unreachable,
    InvalidCapabilities
}

/// <summary>Startup discovery result for one configured plugin.</summary>
internal sealed record ExternalPluginDiscovery(
    string Name,
    Uri BaseUri,
    ExternalDiscoveryStatus Status,
    IReadOnlyList<string> StatusProviderIds,
    IReadOnlyList<ExternalOperationDefinition> Operations,
    IReadOnlyList<ExternalCapabilityExclusion> Exclusions,
    DateTimeOffset ObservedAtUtc);
