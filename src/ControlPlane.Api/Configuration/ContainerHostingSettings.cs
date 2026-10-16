namespace ControlPlane.Api.Configuration;

public sealed class ContainerHostingSettings
{
    public const string SectionName = "ContainerHosting";

    public bool Enabled { get; set; }
}
