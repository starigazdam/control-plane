namespace ControlPlane.HttpEndpoint;

/// <summary>
/// Raised when a configured endpoint file is missing, unreadable, or malformed. The
/// message names the setting and configured path but never an endpoint URL or raw
/// parser/exception text, so it is safe to fail startup with it.
/// </summary>
public sealed class HttpEndpointConfigurationException : Exception
{
    public HttpEndpointConfigurationException(string safeMessage)
        : base(safeMessage)
    {
    }
}
