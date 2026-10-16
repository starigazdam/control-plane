namespace ControlPlane.HttpEndpoint;

/// <summary>
/// Safe results of one shared overview scan, keyed by endpoint ID.
/// </summary>
public sealed record HttpEndpointScanResult(IReadOnlyList<EndpointCheckOutcome> Outcomes);
