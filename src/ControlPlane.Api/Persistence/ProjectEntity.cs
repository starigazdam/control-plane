namespace ControlPlane.Api.Persistence;

public sealed class ProjectEntity
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string TagsJson { get; set; } = "[]";

    public string EnvironmentsJson { get; set; } = "[]";
}
