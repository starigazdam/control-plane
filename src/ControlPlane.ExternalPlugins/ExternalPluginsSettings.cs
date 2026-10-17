namespace ControlPlane.ExternalPlugins;

/// <summary>
/// Binds the <c>ExternalPlugins</c> configuration section. <see cref="Plugins"/> is a
/// static list of entries; an empty list registers no external plugins.
/// </summary>
internal sealed class ExternalPluginsSettings
{
    public const string SectionName = "ExternalPlugins";

    public List<ExternalPluginEntry>? Plugins { get; set; }
}

internal sealed class ExternalPluginEntry
{
    public string? Name { get; set; }

    public string? BaseUrl { get; set; }
}
