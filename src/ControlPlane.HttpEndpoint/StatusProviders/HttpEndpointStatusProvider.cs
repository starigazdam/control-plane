using ControlPlane.Core.Concepts;
using ControlPlane.Core.Interfaces;

namespace ControlPlane.HttpEndpoint.StatusProviders;

/// <summary>
/// Maps shared endpoint scan results to status signals: expected status is
/// <see cref="StatusLevel.Healthy"/>, unexpected status is <see cref="StatusLevel.Warning"/>,
/// timeout or transport failure is <see cref="StatusLevel.Critical"/>. Invalid entries and
/// schedule fields are safe <see cref="StatusLevel.Warning"/> signals. A disabled plugin
/// returns <see cref="StatusLevel.Unknown"/> with no signals.
/// </summary>
public sealed class HttpEndpointStatusProvider : IStatusProvider
{
    public const string ProviderId = "http-endpoint";
    private const string Source = "HTTP Endpoint";

    private readonly HttpEndpointConfiguration _configuration;
    private readonly HttpEndpointScanCoordinator _coordinator;

    public HttpEndpointStatusProvider(
        HttpEndpointConfiguration configuration,
        HttpEndpointScanCoordinator coordinator)
    {
        _configuration = configuration;
        _coordinator = coordinator;
    }

    public string Id => ProviderId;

    public async Task<StatusSnapshot> GetStatusAsync(Project project, CancellationToken cancellationToken)
    {
        var observedAtUtc = DateTimeOffset.UtcNow;

        if (!_configuration.IsEnabled)
        {
            return new StatusSnapshot(
                ProjectId: project.Id,
                ProviderId: Id,
                OverallLevel: StatusLevel.Unknown,
                ObservedAtUtc: observedAtUtc,
                Signals: []);
        }

        var scan = await _coordinator.GetScanAsync(_configuration, cancellationToken).ConfigureAwait(false);

        var signals = new List<StatusSignal>(scan.Outcomes.Count + _configuration.Warnings.Count + 1);
        foreach (var outcome in scan.Outcomes)
        {
            signals.Add(CreateOutcomeSignal(outcome));
        }

        foreach (var warning in _configuration.Warnings)
        {
            signals.Add(CreateWarningSignal(warning, observedAtUtc));
        }

        if (_configuration.HasScheduleWarning)
        {
            signals.Add(CreateScheduleSignal(observedAtUtc));
        }

        return new StatusSnapshot(
            ProjectId: project.Id,
            ProviderId: Id,
            OverallLevel: GetOverallLevel(signals),
            ObservedAtUtc: observedAtUtc,
            Signals: signals);
    }

    private static StatusSignal CreateOutcomeSignal(EndpointCheckOutcome outcome)
    {
        var (title, description) = outcome.Level switch
        {
            StatusLevel.Healthy => ("HTTP endpoint is healthy", "The configured endpoint returned its expected status."),
            StatusLevel.Warning => ("HTTP endpoint returned an unexpected status", "The configured endpoint returned a status other than the expected one."),
            _ => ("HTTP endpoint is unavailable", "The configured endpoint timed out or failed at the transport layer.")
        };

        return new StatusSignal(
            Id: $"{ProviderId}-{outcome.EndpointId}",
            Title: title,
            Description: description,
            Level: outcome.Level,
            ObservedAtUtc: outcome.ObservedAtUtc,
            Source: Source,
            Link: null);
    }

    private static StatusSignal CreateWarningSignal(HttpEndpointConfigWarning warning, DateTimeOffset observedAtUtc)
    {
        return new StatusSignal(
            Id: $"{ProviderId}-config-{warning.Index}",
            Title: "Invalid HTTP endpoint configuration",
            Description: "A configured endpoint entry was excluded during validation.",
            Level: StatusLevel.Warning,
            ObservedAtUtc: observedAtUtc,
            Source: Source,
            Link: null);
    }

    private static StatusSignal CreateScheduleSignal(DateTimeOffset observedAtUtc)
    {
        return new StatusSignal(
            Id: $"{ProviderId}-schedule-ignored",
            Title: "Scheduled HTTP endpoint checks are ignored",
            Description: "Schedule fields are not supported in this release; configured endpoints remain available for manual checks.",
            Level: StatusLevel.Warning,
            ObservedAtUtc: observedAtUtc,
            Source: Source,
            Link: null);
    }

    private static StatusLevel GetOverallLevel(IReadOnlyList<StatusSignal> signals)
    {
        if (signals.Count == 0)
        {
            return StatusLevel.Unknown;
        }

        var level = StatusLevel.Unknown;
        foreach (var signal in signals)
        {
            if (signal.Level > level)
            {
                level = signal.Level;
            }
        }

        return level;
    }
}
