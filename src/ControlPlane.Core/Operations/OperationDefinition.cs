namespace ControlPlane.Core.Operations;

public sealed record OperationDefinition(
    string Id,
    string DisplayName,
    string Description,
    bool RequiresConfirmation,
    IReadOnlyList<OperationParameter> Parameters);
