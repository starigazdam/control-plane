using ControlPlane.Core.Concepts;
using ControlPlane.Core.Interfaces;
using ControlPlane.CopilotAgent.Agent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlPlane.CopilotAgent.StatusProviders;

/// <summary>
/// Checks whether the Copilot CLI is reachable and authenticated by running
/// <c>gh --version</c> as a lightweight, non-interactive probe.
/// Surfaces a <see cref="StatusLevel.Warning"/> when the plugin is disabled
/// or the CLI is unreachable, so the state is always visible on the overview page.
/// </summary>
public sealed class CopilotAgentAvailabilityStatusProvider : IStatusProvider
{
    private readonly CopilotAgentSettings _settings;
    private readonly ICopilotAgentRunner _runner;
    private readonly ILogger<CopilotAgentAvailabilityStatusProvider> _logger;

    public string Id => "copilot-agent-availability";

    public CopilotAgentAvailabilityStatusProvider(
        IOptions<CopilotAgentSettings> settings,
        ICopilotAgentRunner runner,
        ILogger<CopilotAgentAvailabilityStatusProvider> logger)
    {
        _settings = settings.Value;
        _runner = runner;
        _logger = logger;
    }

    public async Task<StatusSnapshot> GetStatusAsync(Project project, CancellationToken cancellationToken)
    {
        var observedAt = DateTimeOffset.UtcNow;

        if (!_settings.Enabled)
        {
            var disabledSignal = new StatusSignal(
                Id: "copilot-agent-disabled",
                Title: "Copilot Agent disabled",
                Description: "Set CopilotAgent__Enabled=true in .env.local to activate agent operations.",
                Level: StatusLevel.Warning,
                ObservedAtUtc: observedAt,
                Source: "Copilot Agent",
                Link: null);

            return new StatusSnapshot(
                ProjectId: project.Id,
                ProviderId: Id,
                OverallLevel: StatusLevel.Warning,
                ObservedAtUtc: observedAt,
                Signals: [disabledSignal]);
        }

        try
        {
            // ProbeAsync runs "gh --version" — a fast, non-interactive check that
            // verifies the CLI executable is present and on PATH. This is intentionally
            // separate from any Copilot subcommand so it doesn't trigger authentication
            // prompts or consume API quota.
            var result = await _runner.ProbeAsync(cancellationToken);

            var (level, title, description) = result.Succeeded
                ? (StatusLevel.Healthy,
                   "Copilot Agent reachable",
                   $"CLI responded in {result.Duration.TotalMilliseconds:F0}ms.")
                : (StatusLevel.Warning,
                   "Copilot Agent unavailable",
                   $"CLI exited with code {result.ExitCode}. {(result.ErrorOutput ?? result.Output).TrimEnd('.')}.");

            var signal = new StatusSignal(
                Id: "copilot-agent-liveness",
                Title: title,
                Description: description,
                Level: level,
                ObservedAtUtc: observedAt,
                Source: "Copilot Agent",
                Link: null);

            return new StatusSnapshot(
                ProjectId: project.Id,
                ProviderId: Id,
                OverallLevel: level,
                ObservedAtUtc: observedAt,
                Signals: [signal]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Copilot Agent availability probe failed.");

            var errorSignal = new StatusSignal(
                Id: "copilot-agent-liveness",
                Title: "Copilot Agent probe failed",
                Description: ex.Message,
                Level: StatusLevel.Warning,
                ObservedAtUtc: observedAt,
                Source: "Copilot Agent",
                Link: null);

            return new StatusSnapshot(
                ProjectId: project.Id,
                ProviderId: Id,
                OverallLevel: StatusLevel.Warning,
                ObservedAtUtc: observedAt,
                Signals: [errorSignal]);
        }
    }
}
