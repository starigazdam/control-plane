namespace ControlPlane.HttpEndpoint;

/// <summary>
/// A safe configuration warning tied to the array index of an excluded entry.
/// It never carries the entry's URL, raw values, or parser text.
/// </summary>
public sealed record HttpEndpointConfigWarning(int Index, string Code);
