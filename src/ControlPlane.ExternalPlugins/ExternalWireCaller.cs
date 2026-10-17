using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ControlPlane.ExternalPlugins;

internal readonly record struct ExternalCallOutcome(bool Succeeded, string Body, string ErrorCode, string Detail)
{
    public static ExternalCallOutcome Success(string body) => new(true, body, string.Empty, string.Empty);

    public static ExternalCallOutcome Failure(string errorCode, string detail) => new(false, string.Empty, errorCode, detail);
}

/// <summary>
/// Sends one bounded request to an external plugin with the shared named client.
/// A fixed timeout is applied with a linked cancellation token; the caller's
/// cancellation propagates as <see cref="OperationCanceledException"/> while the
/// bridge's own timeout maps to a stable outcome.
/// </summary>
internal sealed class ExternalWireCaller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeSpan _timeout;

    public ExternalWireCaller(IHttpClientFactory httpClientFactory, TimeSpan timeout)
    {
        _httpClientFactory = httpClientFactory;
        _timeout = timeout;
    }

    public Task<ExternalCallOutcome> GetAsync(Uri uri, CancellationToken callerToken)
        => SendAsync((client, token) => client.GetAsync(uri, token), callerToken);

    public async Task<ExternalCallOutcome> PostAsync(Uri uri, object payload, CancellationToken callerToken)
    {
        var json = JsonSerializer.Serialize(payload, ExternalJson.Options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await SendAsync((client, token) => client.PostAsync(uri, content, token), callerToken)
            .ConfigureAwait(false);
    }

    private async Task<ExternalCallOutcome> SendAsync(
        Func<HttpClient, CancellationToken, Task<HttpResponseMessage>> send,
        CancellationToken callerToken)
    {
        var client = _httpClientFactory.CreateClient(ExternalPluginsHttpClient.Name);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        timeoutSource.CancelAfter(_timeout);

        try
        {
            using var response = await send(client, timeoutSource.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return ExternalCallOutcome.Failure(
                    ExternalOperationErrorCodes.UnexpectedStatus,
                    $"The external plugin returned HTTP {(int)response.StatusCode}.");
            }

            string body;
            try
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(timeoutSource.Token).ConfigureAwait(false);
                body = Encoding.UTF8.GetString(bytes);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return ExternalCallOutcome.Failure(
                    ExternalOperationErrorCodes.InvalidResponse,
                    "The external plugin response body could not be read.");
            }

            return ExternalCallOutcome.Success(body);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ExternalCallOutcome.Failure(
                ExternalOperationErrorCodes.Timeout,
                "The external operation call timed out.");
        }
        catch (HttpRequestException)
        {
            return ExternalCallOutcome.Failure(
                ExternalOperationErrorCodes.TransportFailed,
                "The external operation call failed at the transport layer.");
        }
    }
}
