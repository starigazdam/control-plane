namespace ControlPlane.Core.Operations;

public sealed record OperationExecutionContext(
    OperationRequest Request,
    string InitiatedBy,
    DateTimeOffset RequestedAtUtc,
    string? CorrelationId);
