namespace ControlPlane.HttpEndpoint;

/// <summary>
/// Immutable snapshot of the configured endpoints and the safe warnings produced while
/// validating the configuration file.
/// </summary>
public sealed class HttpEndpointConfiguration
{
    public static readonly HttpEndpointConfiguration Disabled =
        new(isEnabled: false, endpoints: [], warnings: [], hasScheduleWarning: false);

    public HttpEndpointConfiguration(
        bool isEnabled,
        IReadOnlyList<HttpEndpointDefinition> endpoints,
        IReadOnlyList<HttpEndpointConfigWarning> warnings,
        bool hasScheduleWarning)
    {
        IsEnabled = isEnabled;
        Endpoints = endpoints;
        Warnings = warnings;
        HasScheduleWarning = hasScheduleWarning;
    }

    public bool IsEnabled { get; }

    public IReadOnlyList<HttpEndpointDefinition> Endpoints { get; }

    public IReadOnlyList<HttpEndpointConfigWarning> Warnings { get; }

    public bool HasScheduleWarning { get; }

    public HttpEndpointDefinition? FindById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var trimmed = id.Trim();
        foreach (var endpoint in Endpoints)
        {
            if (string.Equals(endpoint.Id, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return endpoint;
            }
        }

        return null;
    }
}
