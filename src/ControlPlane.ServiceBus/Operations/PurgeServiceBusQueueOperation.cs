using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Operations;

namespace ControlPlane.ServiceBus.Operations;

public sealed class PurgeServiceBusQueueOperation : IOperation
{
    public OperationDefinition Definition => new(
        Id: "purge-servicebus-queue",
        DisplayName: "Purge Service Bus Queue",
        Description: "Purges pending messages from the selected Service Bus queue.",
        RequiresConfirmation: true,
        Parameters:
        [
            new OperationParameter(
                Name: "queueName",
                DisplayName: "Queue Name",
                Description: "Queue to purge.",
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
        if (!context.Request.Input.TryGetValue("queueName", out var queueName) || string.IsNullOrWhiteSpace(queueName))
        {
            errors.Add("The 'queueName' parameter is required.");
        }

        return Task.FromResult<IReadOnlyList<string>>(errors);
    }

    public Task<OperationExecutionResult> ExecuteAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startedAtUtc = DateTimeOffset.UtcNow;
        var queueName = context.Request.Input["queueName"]!;
        var output = new Dictionary<string, string?>
        {
            ["queueName"] = queueName,
            ["executionMode"] = "stub",
            ["note"] = "Wire this operation to Service Bus queue purge logic."
        };

        return Task.FromResult(
            new OperationExecutionResult(
                Status: OperationExecutionStatus.Succeeded,
                Message: $"Purge request accepted for Service Bus queue '{queueName}'.",
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                ErrorCode: null,
                Output: output));
    }
}
