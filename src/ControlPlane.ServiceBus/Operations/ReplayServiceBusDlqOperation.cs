using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Operations;

namespace ControlPlane.ServiceBus.Operations;

public sealed class ReplayServiceBusDlqOperation : IOperation
{
    public OperationDefinition Definition => new(
        Id: "replay-servicebus-dlq",
        DisplayName: "Replay Service Bus DLQ",
        Description: "Replays dead-letter messages from a Service Bus entity.",
        RequiresConfirmation: true,
        Parameters:
        [
            new OperationParameter(
                Name: "entityPath",
                DisplayName: "Entity Path",
                Description: "Queue or subscription path to replay.",
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
        if (!context.Request.Input.TryGetValue("entityPath", out var entityPath) || string.IsNullOrWhiteSpace(entityPath))
        {
            errors.Add("The 'entityPath' parameter is required.");
        }

        return Task.FromResult<IReadOnlyList<string>>(errors);
    }

    public Task<OperationExecutionResult> ExecuteAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startedAtUtc = DateTimeOffset.UtcNow;
        var entityPath = context.Request.Input["entityPath"]!;
        var output = new Dictionary<string, string?>
        {
            ["entityPath"] = entityPath,
            ["executionMode"] = "stub",
            ["note"] = "Wire this operation to Service Bus replay logic."
        };

        return Task.FromResult(
            new OperationExecutionResult(
                Status: OperationExecutionStatus.Succeeded,
                Message: $"Replay request accepted for Service Bus entity '{entityPath}'.",
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                ErrorCode: null,
                Output: output));
    }
}
