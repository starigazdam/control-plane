namespace ControlPlane.CopilotAgent.Agent;

/// <summary>
/// Outcome of a single Copilot CLI invocation.
/// </summary>
public sealed record AgentInvocationResult(
    bool Succeeded,
    string Output,
    string? ErrorOutput,
    int ExitCode,
    TimeSpan Duration);
