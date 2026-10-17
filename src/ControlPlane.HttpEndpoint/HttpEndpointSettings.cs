namespace ControlPlane.HttpEndpoint;

/// <summary>
/// Configuration for the HTTP endpoint plugin, bound from the <c>HttpEndpoint</c> section.
/// </summary>
public sealed class HttpEndpointSettings
{
    public const string SectionName = "HttpEndpoint";

    /// <summary>
    /// Path to the JSON file that lists the configured endpoints. Relative paths are
    /// resolved from the API content root. An unset or empty value disables the plugin.
    /// </summary>
    public string ConfigPath { get; set; } = string.Empty;

    /// <summary>
    /// Default cron expression. It is deliberately ignored in this slice: scheduled
    /// checks are out of scope and are never parsed or started.
    /// </summary>
    public string DefaultCron { get; set; } = string.Empty;
}
