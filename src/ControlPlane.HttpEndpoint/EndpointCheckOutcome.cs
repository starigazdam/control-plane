using ControlPlane.Core.Concepts;

namespace ControlPlane.HttpEndpoint;

/// <summary>
/// Safe result of a single endpoint check. It never carries the endpoint URL, response
/// headers, response body, display name, or exception text.
/// </summary>
public sealed record EndpointCheckOutcome(
    string EndpointId,
    StatusLevel Level,
    int? ObservedStatus,
    DateTimeOffset ObservedAtUtc,
    TimeSpan Duration,
    bool Succeeded,
    string? ErrorCode)
{
    public const string UnexpectedStatus = "endpoint_unexpected_status";
    public const string Timeout = "endpoint_timeout";
    public const string RequestFailed = "endpoint_request_failed";

    public static EndpointCheckOutcome Healthy(
        string endpointId,
        int observedStatus,
        DateTimeOffset observedAtUtc,
        TimeSpan duration)
        => new(endpointId, StatusLevel.Healthy, observedStatus, observedAtUtc, duration, Succeeded: true, ErrorCode: null);

    public static EndpointCheckOutcome UnexpectedStatusResult(
        string endpointId,
        int observedStatus,
        DateTimeOffset observedAtUtc,
        TimeSpan duration)
        => new(endpointId, StatusLevel.Warning, observedStatus, observedAtUtc, duration, Succeeded: false, UnexpectedStatus);

    public static EndpointCheckOutcome TimedOut(
        string endpointId,
        DateTimeOffset observedAtUtc,
        TimeSpan duration)
        => new(endpointId, StatusLevel.Critical, ObservedStatus: null, observedAtUtc, duration, Succeeded: false, Timeout);

    public static EndpointCheckOutcome Failed(
        string endpointId,
        DateTimeOffset observedAtUtc,
        TimeSpan duration)
        => new(endpointId, StatusLevel.Critical, ObservedStatus: null, observedAtUtc, duration, Succeeded: false, RequestFailed);
}
