using ControlPlane.Core.Operations;

namespace ControlPlane.Api.Contracts;

public sealed record OperationHistoryEntry(
    string ProjectId,
    string OperationId,
    string InitiatedBy,
    DateTimeOffset RequestedAtUtc,
    string? CorrelationId,
    OperationExecutionResult Result);
