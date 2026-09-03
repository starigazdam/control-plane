namespace ControlPlane.Api.Configuration;

public sealed class ProjectSettings
{
    public const string SectionName = "Project";

    public const string DefaultName = "Control Plane";

    public string Name { get; set; } = string.Empty;
}
