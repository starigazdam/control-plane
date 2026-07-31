namespace ControlPlane.Core.Operations;

public sealed record OperationRequest(
    string ProjectId,
    string OperationId,
    IReadOnlyDictionary<string, string?> Input);
