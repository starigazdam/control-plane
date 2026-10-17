using ControlPlane.Core.Concepts;
using ControlPlane.Core.Interfaces;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger? _logger;
    private readonly string _pluginName;
    private readonly string? _failureReason;
    private int _failureLogged;

    public ExternalBridgeStatusProvider(ExternalPluginDiscovery discovery)
        : this(discovery, logger: null)
    {
    }

    public ExternalBridgeStatusProvider(ExternalPluginDiscovery discovery, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(discovery);

        Id = $"{discovery.Name}.bridge";
        _pluginName = discovery.Name;
        ObservedAtUtc = discovery.ObservedAtUtc;
        _logger = logger;
        _failureReason = discovery.Status switch
        {
            ExternalDiscoveryStatus.Unreachable => ExternalBridgeSignals.Unreachable,
            ExternalDiscoveryStatus.InvalidCapabilities => ExternalBridgeSignals.InvalidCapabilities,
            _ => null
        };
        (_overallLevel, _signals) = BuildOutcome(discovery);
    }

    public string Id { get; }

    private DateTimeOffset ObservedAtUtc { get; }

    public Task<StatusSnapshot> GetStatusAsync(Project project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        LogDiscoveryFailureOnce();
        return Task.FromResult(new StatusSnapshot(
            project.Id,
            Id,
            _overallLevel,
            ObservedAtUtc,
            _signals));
    }

    private void LogDiscoveryFailureOnce()
    {
        if (_failureReason is null || _logger is null || Interlocked.Exchange(ref _failureLogged, 1) != 0)
        {
            return;
        }

        _logger.LogWarning(
            "External plugin '{PluginName}' discovery failed: {Reason}.",
            _pluginName,
            _failureReason);
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
        for (var index = 0; index < discovery.Exclusions.Count; index++)
        {
            var exclusion = discovery.Exclusions[index];
            signals.Add(new StatusSignal(
                $"excluded-{exclusion.Kind}-{exclusion.Identifier}-{index}",
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
