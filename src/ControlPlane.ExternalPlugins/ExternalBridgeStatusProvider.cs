using ControlPlane.Core.Concepts;
using ControlPlane.Core.Interfaces;

namespace ControlPlane.ExternalPlugins;

/// <summary>
/// Reports the startup discovery result for one configured plugin and never
/// changes afterwards. It is <see cref="StatusLevel.Healthy"/> on clean discovery,
/// <see cref="StatusLevel.Warning"/> with the fixed <c>unreachable</c> or
/// <c>invalid-capabilities</c> signal on failure, or <see cref="StatusLevel.Warning"/>
/// with one signal per excluded capability.
/// </summary>
internal sealed class ExternalBridgeStatusProvider : IStatusProvider
{
    private const string Source = "External Plugin";

    private readonly StatusLevel _overallLevel;
    private readonly IReadOnlyList<StatusSignal> _signals;

    public ExternalBridgeStatusProvider(ExternalPluginDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);

        Id = $"{discovery.Name}.bridge";
        ObservedAtUtc = discovery.ObservedAtUtc;
        (_overallLevel, _signals) = BuildOutcome(discovery);
    }

    public string Id { get; }

    private DateTimeOffset ObservedAtUtc { get; }

    public Task<StatusSnapshot> GetStatusAsync(Project project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Task.FromResult(new StatusSnapshot(
            project.Id,
            Id,
            _overallLevel,
            ObservedAtUtc,
            _signals));
    }

    private static (StatusLevel Level, IReadOnlyList<StatusSignal> Signals) BuildOutcome(
        ExternalPluginDiscovery discovery)
    {
        switch (discovery.Status)
        {
            case ExternalDiscoveryStatus.Unreachable:
                return (StatusLevel.Warning,
                [
                    new StatusSignal(
                        ExternalBridgeSignals.Unreachable,
                        "External plugin is unreachable",
                        "The external plugin did not answer its capabilities request at startup.",
                        StatusLevel.Warning,
                        discovery.ObservedAtUtc,
                        Source,
                        Link: null)
                ]);

            case ExternalDiscoveryStatus.InvalidCapabilities:
                return (StatusLevel.Warning,
                [
                    new StatusSignal(
                        ExternalBridgeSignals.InvalidCapabilities,
                        "External plugin returned invalid capabilities",
                        "The external plugin returned a non-2xx response or a body that is not valid capabilities.",
                        StatusLevel.Warning,
                        discovery.ObservedAtUtc,
                        Source,
                        Link: null)
                ]);
        }

        if (discovery.Exclusions.Count == 0)
        {
            return (StatusLevel.Healthy, []);
        }

        var signals = new List<StatusSignal>(discovery.Exclusions.Count);
        foreach (var exclusion in discovery.Exclusions)
        {
            signals.Add(new StatusSignal(
                $"excluded-{exclusion.Kind}-{exclusion.Identifier}",
                "External capability was excluded",
                exclusion.Reason,
                StatusLevel.Warning,
                discovery.ObservedAtUtc,
                Source,
                Link: null));
        }

        return (StatusLevel.Warning, signals);
    }
}
