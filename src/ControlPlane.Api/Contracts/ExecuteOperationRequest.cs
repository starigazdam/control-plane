namespace ControlPlane.Api.Contracts;

public sealed record ExecuteOperationRequest(
    string ProjectId,
    string OperationId,
    IReadOnlyDictionary<string, string?>? Input,
    string? RequestedBy,
    string? CorrelationId);
