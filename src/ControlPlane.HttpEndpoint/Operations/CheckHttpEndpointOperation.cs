using System.Globalization;
using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Operations;

namespace ControlPlane.HttpEndpoint.Operations;

/// <summary>
/// Manual check operation. It accepts exactly one configured endpoint ID and never a
/// URL, request body, or caller-supplied headers. The persisted summary contains only
/// the endpoint ID, observed status, observation time, elapsed duration, and a stable
/// error code.
/// </summary>
public sealed class CheckHttpEndpointOperation : IOperation
{
    public const string OperationId = "http-endpoint-check";
    public const string ParameterName = "endpointId";

    private const string UnknownEndpointMessage =
        "The 'endpointId' value does not match a configured HTTP endpoint.";

    private readonly HttpEndpointConfiguration _configuration;
    private readonly HttpEndpointChecker _checker;

    public CheckHttpEndpointOperation(HttpEndpointConfiguration configuration, HttpEndpointChecker checker)
    {
        _configuration = configuration;
        _checker = checker;
    }

    public OperationDefinition Definition => new(
        Id: "http-endpoint-check",
        DisplayName: "Check HTTP Endpoint",
        Description: "Checks one configured HTTP endpoint by ID using its configured GET or HEAD method and returns a safe summary. It accepts no URL, request body, or custom headers.",
        RequiresConfirmation: false,
        Parameters:
        [
            new OperationParameter(
                Name: ParameterName,
                DisplayName: "Endpoint ID",
                Description: "ID of a configured HTTP endpoint to check.",
                IsRequired: true,
                Type: "string",
                DefaultValue: null)
        ]);

    public Task<IReadOnlyList<string>> ValidateAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var errors = new List<string>();
        if (!context.Request.Input.TryGetValue(ParameterName, out var endpointId) || string.IsNullOrWhiteSpace(endpointId))
        {
            errors.Add("The 'endpointId' parameter is required.");
        }
        else if (_configuration.FindById(endpointId) is null)
        {
            errors.Add(UnknownEndpointMessage);
        }

        return Task.FromResult<IReadOnlyList<string>>(errors);
    }

    public async Task<OperationExecutionResult> ExecuteAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;

        if (!context.Request.Input.TryGetValue(ParameterName, out var endpointId)
            || string.IsNullOrWhiteSpace(endpointId)
            || _configuration.FindById(endpointId) is not { } definition)
        {
            return new OperationExecutionResult(
                Status: OperationExecutionStatus.Rejected,
                Message: UnknownEndpointMessage,
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                ErrorCode: "operation_validation_failed",
                Output: new Dictionary<string, string?>());
        }

        var outcome = await _checker.CheckAsync(definition, cancellationToken).ConfigureAwait(false);

        var output = new Dictionary<string, string?>
        {
            ["endpointId"] = outcome.EndpointId,
            ["observedStatus"] = outcome.ObservedStatus?.ToString(CultureInfo.InvariantCulture),
            ["observedAtUtc"] = outcome.ObservedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ["durationMs"] = outcome.Duration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture)
        };

        var message = outcome.ErrorCode switch
        {
            null => "The configured endpoint check succeeded.",
            EndpointCheckOutcome.UnexpectedStatus => "The configured endpoint returned an unexpected status.",
            EndpointCheckOutcome.Timeout => "The configured endpoint check timed out.",
            _ => "The configured endpoint request failed."
        };

        return new OperationExecutionResult(
            Status: outcome.Succeeded ? OperationExecutionStatus.Succeeded : OperationExecutionStatus.Failed,
            Message: message,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            ErrorCode: outcome.ErrorCode,
            Output: output);
    }
}
