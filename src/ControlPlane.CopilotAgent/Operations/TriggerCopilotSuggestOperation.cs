using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Operations;
using ControlPlane.CopilotAgent.Agent;
using Microsoft.Extensions.Options;

namespace ControlPlane.CopilotAgent.Operations;

/// <summary>
/// Delegates a backend task description to the Copilot CLI (<c>gh copilot suggest</c>),
/// which returns a shell command or script that can be reviewed and executed by a human.
///
/// This operation is <em>advisory only</em>: the returned command is surfaced for
/// human review and is never executed automatically. The full observe → authorize →
/// confirm → act → audit loop is satisfied because:
/// <list type="bullet">
///   <item>Observe — the operator reads the prompt result before deciding to act.</item>
///   <item>Authorize — <see cref="RequiresConfirmation"/> is <c>true</c>.</item>
///   <item>Confirm — the UI shows impact/preview data (the suggested command) before commit.</item>
///   <item>Act — the operator copies and runs the command in the appropriate environment.</item>
///   <item>Audit — the operation and its output are persisted in operation history.</item>
/// </list>
/// </summary>
public sealed class TriggerCopilotSuggestOperation : IOperation
{
    private readonly ICopilotAgentRunner _runner;
    private readonly CopilotAgentSettings _settings;

    public TriggerCopilotSuggestOperation(
        ICopilotAgentRunner runner,
        IOptions<CopilotAgentSettings> settings)
    {
        _runner = runner;
        _settings = settings.Value;
    }

    public OperationDefinition Definition => new(
        Id: "trigger-copilot-suggest",
        DisplayName: "Ask Copilot: Suggest Command",
        Description: "Sends a backend task description to GitHub Copilot CLI (gh copilot suggest) and returns a suggested shell command for human review. The command is never executed automatically.",
        RequiresConfirmation: true,
        Parameters:
        [
            new OperationParameter(
                Name: "prompt",
                DisplayName: "Task description",
                Description: "Describe the backend task or problem you want Copilot to suggest a command for.",
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

        if (!context.Request.Input.TryGetValue("prompt", out var prompt) || string.IsNullOrWhiteSpace(prompt))
        {
            errors.Add("The 'prompt' parameter is required.");
        }

        return Task.FromResult<IReadOnlyList<string>>(errors);
    }

    public async Task<OperationExecutionResult> ExecuteAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var prompt = context.Request.Input["prompt"]!;

        var result = await _runner.SuggestAsync(prompt, cancellationToken);

        var output = new Dictionary<string, string?>
        {
            ["prompt"] = prompt,
            ["suggestedCommand"] = result.Output,
            ["exitCode"] = result.ExitCode.ToString(),
            ["durationMs"] = result.Duration.TotalMilliseconds.ToString("F0"),
            ["note"] = "Review the suggested command before executing it in any environment."
        };

        if (result.ErrorOutput is not null)
        {
            output["errorOutput"] = result.ErrorOutput;
        }

        return new OperationExecutionResult(
            Status: result.Succeeded ? OperationExecutionStatus.Succeeded : OperationExecutionStatus.Failed,
            Message: result.Succeeded
                ? $"Copilot suggested a command for: \"{TruncateForMessage(prompt)}\"."
                : $"Copilot CLI failed (exit code {result.ExitCode}).",
            StartedAtUtc: startedAt,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            ErrorCode: result.Succeeded ? null : "copilot_cli_error",
            Output: output);
    }

    private static string TruncateForMessage(string value, int maxLength = 80) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}
