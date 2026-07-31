namespace ControlPlane.Core.Operations;

public sealed record OperationExecutionResult(
    OperationExecutionStatus Status,
    string Message,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string? ErrorCode,
    IReadOnlyDictionary<string, string?> Output);
