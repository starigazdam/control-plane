namespace ControlPlane.DevOps;

public sealed class DevOpsSettings
{
    public const string SectionName = "DevOps";

    public string OrgUrl { get; set; } = string.Empty;
    public string PersonalAccessToken { get; set; } = string.Empty;

    /// <summary>Comma-separated list of Azure DevOps project names to monitor.</summary>
    public string MonitoredProjects { get; set; } = string.Empty;

    /// <summary>How far back (in hours) to look for failed pipeline runs.</summary>
    public int LookbackHours { get; set; } = 24;
}
