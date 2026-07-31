namespace ControlPlane.Core.Concepts;

public sealed record Project(
    string Id,
    string Name,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Environments);
