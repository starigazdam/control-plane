using System.Diagnostics;
using ControlPlane.Core.Concepts;

namespace ControlPlane.HttpEndpoint;

/// <summary>
/// Sends at most one request per check using a single named
/// <see cref="System.Net.Http.IHttpClientFactory"/> client. The named client disables
/// retries, circuit breaking, redirects, service discovery, framework request logging,
/// URL-bearing trace data, and any second timeout layer; the per-entry timeout is
/// enforced here with a linked cancellation token.
/// </summary>
public sealed class HttpEndpointChecker
{
    /// <summary>Logical name of the dedicated client used for endpoint checks.</summary>
    public const string HttpClientName = "http-endpoint";

    /// <summary>
    /// Request option marker that lets the shared telemetry pipeline recognize this
    /// client's requests and suppress URL-derived trace data. The literal value is
    /// mirrored in <c>ControlPlane.ServiceDefaults</c>.
    /// </summary>
    public const string RequestMarkerOptionKey = "ControlPlane.HttpEndpoint.Check";

    private readonly IHttpClientFactory _httpClientFactory;

    public HttpEndpointChecker(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<EndpointCheckOutcome> CheckAsync(
        HttpEndpointDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        cancellationToken.ThrowIfCancellationRequested();

        var observedAtUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(definition.Timeout);

        using var request = new HttpRequestMessage(new HttpMethod(definition.Method), definition.Url);
        request.Options.Set(new HttpRequestOptionsKey<bool>(RequestMarkerOptionKey), true);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token)
                .ConfigureAwait(false);

            stopwatch.Stop();
            var observedStatus = (int)response.StatusCode;

            return observedStatus == definition.ExpectedStatus
                ? EndpointCheckOutcome.Healthy(definition.Id, observedStatus, observedAtUtc, stopwatch.Elapsed)
                : EndpointCheckOutcome.UnexpectedStatusResult(definition.Id, observedStatus, observedAtUtc, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return EndpointCheckOutcome.TimedOut(definition.Id, observedAtUtc, stopwatch.Elapsed);
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();
            return EndpointCheckOutcome.Failed(definition.Id, observedAtUtc, stopwatch.Elapsed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            return EndpointCheckOutcome.Failed(definition.Id, observedAtUtc, stopwatch.Elapsed);
        }
    }
}
