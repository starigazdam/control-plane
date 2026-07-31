namespace ControlPlane.Core.Operations;

public sealed record OperationParameter(
    string Name,
    string DisplayName,
    string Description,
    bool IsRequired,
    string Type,
    string? DefaultValue);
