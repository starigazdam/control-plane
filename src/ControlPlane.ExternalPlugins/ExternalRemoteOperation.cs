using System.Net.Http;
using System.Text.Json;
using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Operations;

namespace ControlPlane.ExternalPlugins;

/// <summary>
/// Normalizes one remote operation. A failed call maps to a stable bridge error
/// code: timeout, transport failure, unexpected status, or an invalid response. A
/// well-formed result passes the remote status, message, errorCode, and output
/// through unchanged, with timestamps taken from the bridge's own call timing.
/// </summary>
internal sealed class ExternalRemoteOperation : IOperation
{
    private readonly string _remoteId;
    private readonly Uri _baseUri;
    private readonly ExternalWireCaller _caller;

    public ExternalRemoteOperation(
        string pluginName,
        ExternalOperationDefinition definition,
        Uri baseUri,
        IHttpClientFactory httpClientFactory,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(definition);

        _remoteId = definition.RemoteId;
        _baseUri = baseUri;
        _caller = new ExternalWireCaller(httpClientFactory, timeout);

        Definition = new OperationDefinition(
            $"{pluginName}.{definition.RemoteId}",
            definition.DisplayName,
            definition.Description,
            definition.RequiresConfirmation,
            definition.Parameters);
    }

    public OperationDefinition Definition { get; }

    public async Task<IReadOnlyList<string>> ValidateAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        var uri = ExternalEndpointUri.Validate(_baseUri, _remoteId);
        var payload = new ExternalOperationRequest(context.Request.ProjectId, context.Request.Input);
        var outcome = await _caller.PostAsync(uri, payload, cancellationToken).ConfigureAwait(false);

        if (!outcome.Succeeded)
        {
            return [Format(outcome.ErrorCode, outcome.Detail)];
        }

        try
        {
            var errors = JsonSerializer.Deserialize<List<string>>(outcome.Body, ExternalJson.Options);
            if (errors is null)
            {
                return [Format(ExternalOperationErrorCodes.InvalidResponse, "The remote validate response was empty.")];
            }

            return errors;
        }
        catch (JsonException)
        {
            return
            [
                Format(
                    ExternalOperationErrorCodes.InvalidResponse,
                    "The remote validate response was not a JSON array of strings.")
            ];
        }
    }

    public async Task<OperationExecutionResult> ExecuteAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var uri = ExternalEndpointUri.Execute(_baseUri, _remoteId);
        var payload = new ExternalOperationRequest(context.Request.ProjectId, context.Request.Input);
        var outcome = await _caller.PostAsync(uri, payload, cancellationToken).ConfigureAwait(false);

        if (!outcome.Succeeded)
        {
            return Failed(outcome.ErrorCode, outcome.Detail, startedAtUtc);
        }

        try
        {
            var response = JsonSerializer.Deserialize<ExternalExecuteResponse>(outcome.Body, ExternalJson.Options);
            if (response?.Status is null || response.Message is null)
            {
                return Failed(
                    ExternalOperationErrorCodes.InvalidResponse,
                    "The remote execute response is missing a required field.",
                    startedAtUtc);
            }

            if (!TryParseStatus(response.Status, out var status))
            {
                return Failed(
                    ExternalOperationErrorCodes.InvalidResponse,
                    $"The remote execute response carried an unknown status '{response.Status}'.",
                    startedAtUtc);
            }

            if (response.ErrorCode is not null
                && response.ErrorCode.StartsWith(ExternalOperationErrorCodes.Prefix, StringComparison.Ordinal))
            {
                return Failed(
                    ExternalOperationErrorCodes.InvalidResponse,
                    "The remote execute response used a reserved bridge error code.",
                    startedAtUtc);
            }

            if (!TryBuildOutput(response.Output, out var output))
            {
                return Failed(
                    ExternalOperationErrorCodes.InvalidResponse,
                    "The remote execute response carried an output value that is not a string or null.",
                    startedAtUtc);
            }

            return new OperationExecutionResult(
                status,
                response.Message,
                startedAtUtc,
                DateTimeOffset.UtcNow,
                string.IsNullOrEmpty(response.ErrorCode) ? null : response.ErrorCode,
                output);
        }
        catch (JsonException)
        {
            return Failed(
                ExternalOperationErrorCodes.InvalidResponse,
                "The remote execute response was not a well-formed JSON result.",
                startedAtUtc);
        }
    }

    private static string Format(string errorCode, string detail) => $"{errorCode}: {detail}";

    private OperationExecutionResult Failed(string errorCode, string detail, DateTimeOffset startedAtUtc)
        => new(
            OperationExecutionStatus.Failed,
            detail,
            startedAtUtc,
            DateTimeOffset.UtcNow,
            errorCode,
            new Dictionary<string, string?>());

    private static bool TryParseStatus(string status, out OperationExecutionStatus parsed)
    {
        if (string.Equals(status, nameof(OperationExecutionStatus.Succeeded), StringComparison.OrdinalIgnoreCase))
        {
            parsed = OperationExecutionStatus.Succeeded;
            return true;
        }

        if (string.Equals(status, nameof(OperationExecutionStatus.Failed), StringComparison.OrdinalIgnoreCase))
        {
            parsed = OperationExecutionStatus.Failed;
            return true;
        }

        if (string.Equals(status, nameof(OperationExecutionStatus.Rejected), StringComparison.OrdinalIgnoreCase))
        {
            parsed = OperationExecutionStatus.Rejected;
            return true;
        }

        parsed = default;
        return false;
    }

    private static bool TryBuildOutput(
        Dictionary<string, JsonElement>? output,
        out IReadOnlyDictionary<string, string?> result)
    {
        var map = new Dictionary<string, string?>();
        if (output is not null)
        {
            foreach (var (key, value) in output)
            {
                if (value.ValueKind == JsonValueKind.Null)
                {
                    map[key] = null;
                }
                else if (value.ValueKind == JsonValueKind.String)
                {
                    map[key] = value.GetString();
                }
                else
                {
                    result = map;
                    return false;
                }
            }
        }

        result = map;
        return true;
    }

    private sealed class ExternalExecuteResponse
    {
        public string? Status { get; set; }

        public string? Message { get; set; }

        public string? ErrorCode { get; set; }

        public Dictionary<string, JsonElement>? Output { get; set; }
    }
}

internal sealed record ExternalOperationRequest(string ProjectId, IReadOnlyDictionary<string, string?> Input);
