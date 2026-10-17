using System.Net.Http;
using System.Text.Json;
using ControlPlane.Core.Concepts;
using ControlPlane.Core.Interfaces;

namespace ControlPlane.ExternalPlugins;

/// <summary>
/// Normalizes one remote status provider. A failed call never throws: it returns a
/// <see cref="StatusLevel.Warning"/> snapshot with the fixed <c>status-call-failed</c>
/// signal. Every returned snapshot carries the namespaced <c>providerId</c> and the
/// requested <c>projectId</c>.
/// </summary>
internal sealed class ExternalRemoteStatusProvider : IStatusProvider
{
    private readonly string _remoteId;
    private readonly Uri _baseUri;
    private readonly ExternalWireCaller _caller;

    public ExternalRemoteStatusProvider(
        string pluginName,
        string remoteId,
        Uri baseUri,
        IHttpClientFactory httpClientFactory,
        TimeSpan timeout)
    {
        Id = $"{pluginName}.{remoteId}";
        _remoteId = remoteId;
        _baseUri = baseUri;
        _caller = new ExternalWireCaller(httpClientFactory, timeout);
    }

    public string Id { get; }

    public async Task<StatusSnapshot> GetStatusAsync(Project project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);

        var uri = ExternalEndpointUri.Status(_baseUri, _remoteId, project.Id);
        var outcome = await _caller.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        if (!outcome.Succeeded)
        {
            return Failed(project, outcome.Detail);
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<StatusSnapshot>(outcome.Body, ExternalJson.Options);
            if (snapshot is null)
            {
                return Failed(project, "The external status response was empty.");
            }

            return snapshot with { ProviderId = Id, ProjectId = project.Id };
        }
        catch (JsonException)
        {
            return Failed(project, "The external status response did not deserialize to a StatusSnapshot.");
        }
    }

    private StatusSnapshot Failed(Project project, string detail)
    {
        var observedAt = DateTimeOffset.UtcNow;
        return new StatusSnapshot(
            project.Id,
            Id,
            StatusLevel.Warning,
            observedAt,
            [
                new StatusSignal(
                    ExternalBridgeSignals.StatusCallFailed,
                    "External status call failed",
                    detail,
                    StatusLevel.Warning,
                    observedAt,
                    Source: null,
                    Link: null)
            ]);
    }
}
