namespace ControlPlane.HttpEndpoint;

/// <summary>
/// A validated, request-ready endpoint definition. Only the ID is ever surfaced to
/// callers; the URL is never returned, logged, or traced.
/// </summary>
public sealed record HttpEndpointDefinition(
    string Id,
    string Name,
    string Url,
    int ExpectedStatus,
    string Method,
    TimeSpan Timeout);
