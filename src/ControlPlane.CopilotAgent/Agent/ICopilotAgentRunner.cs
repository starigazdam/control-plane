namespace ControlPlane.CopilotAgent.Agent;

/// <summary>
/// Runs Copilot CLI tasks on behalf of the Control Plane.
/// Abstracted behind an interface so operations are testable without a real CLI.
/// </summary>
public interface ICopilotAgentRunner
{
    /// <summary>
    /// Checks whether the Copilot CLI is reachable and authenticated by running
    /// <c>gh copilot --version</c> (or equivalent).
    /// Returns a result whose <see cref="AgentInvocationResult.Succeeded"/> indicates
    /// whether the CLI responded without error.
    /// </summary>
    Task<AgentInvocationResult> ProbeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Suggests a shell command or explanation for the given <paramref name="prompt"/> using
    /// <c>gh copilot suggest</c> and returns the raw CLI output.
    /// </summary>
    Task<AgentInvocationResult> SuggestAsync(
        string prompt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asks an open-ended question using <c>gh copilot explain</c> and returns the raw CLI output.
    /// </summary>
    Task<AgentInvocationResult> ExplainAsync(
        string subject,
        CancellationToken cancellationToken);
}
