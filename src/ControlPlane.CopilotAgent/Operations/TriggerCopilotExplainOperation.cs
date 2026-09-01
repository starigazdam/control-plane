using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Operations;
using ControlPlane.CopilotAgent.Agent;
using Microsoft.Extensions.Options;

namespace ControlPlane.CopilotAgent.Operations;

/// <summary>
/// Asks the Copilot CLI to explain a command, error, or concept using
/// <c>gh copilot explain</c>.  Useful for understanding an unfamiliar error
/// message or script before deciding how to act on it.
///
/// Read-only and non-destructive; <see cref="RequiresConfirmation"/> is <c>false</c>.
/// </summary>
public sealed class TriggerCopilotExplainOperation : IOperation
{
    private readonly ICopilotAgentRunner _runner;
    private readonly CopilotAgentSettings _settings;

    public TriggerCopilotExplainOperation(
        ICopilotAgentRunner runner,
        IOptions<CopilotAgentSettings> settings)
    {
        _runner = runner;
        _settings = settings.Value;
    }

    public OperationDefinition Definition => new(
        Id: "trigger-copilot-explain",
        DisplayName: "Ask Copilot: Explain",
        Description: "Sends a command, error message, or concept to GitHub Copilot CLI (gh copilot explain) and returns a plain-language explanation.",
        RequiresConfirmation: false,
        Parameters:
        [
            new OperationParameter(
                Name: "subject",
                DisplayName: "Command or error to explain",
                Description: "The shell command, error output, or concept you want Copilot to explain.",
                IsRequired: true,
                Type: "string",
                DefaultValue: null)
        ]);

    public Task<IReadOnlyList<string>> ValidateAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var errors = new List<string>();

        if (!_settings.Enabled)
        {
            errors.Add("Copilot Agent is disabled. Set CopilotAgent__Enabled=true in .env.local to activate this operation.");
        }

        if (!context.Request.Input.TryGetValue("subject", out var subject) || string.IsNullOrWhiteSpace(subject))
        {
            errors.Add("The 'subject' parameter is required.");
        }

        return Task.FromResult<IReadOnlyList<string>>(errors);
    }

    public async Task<OperationExecutionResult> ExecuteAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var subject = context.Request.Input["subject"]!;

        var result = await _runner.ExplainAsync(subject, cancellationToken);

        var output = new Dictionary<string, string?>
        {
            ["subject"] = subject,
            ["explanation"] = result.Output,
            ["exitCode"] = result.ExitCode.ToString(),
            ["durationMs"] = result.Duration.TotalMilliseconds.ToString("F0")
        };

        if (result.ErrorOutput is not null)
        {
            output["errorOutput"] = result.ErrorOutput;
        }

        return new OperationExecutionResult(
            Status: result.Succeeded ? OperationExecutionStatus.Succeeded : OperationExecutionStatus.Failed,
            Message: result.Succeeded
                ? $"Copilot explained: \"{TruncateForMessage(subject)}\"."
                : $"Copilot CLI failed (exit code {result.ExitCode}).",
            StartedAtUtc: startedAt,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            ErrorCode: result.Succeeded ? null : "copilot_cli_error",
            Output: output);
    }

    private static string TruncateForMessage(string value, int maxLength = 80) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}
