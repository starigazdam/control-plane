using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Operations;

namespace ControlPlane.ServiceBus.Operations;

public sealed class ResendServiceBusDlqToQueueOperation : IOperation
{
    public OperationDefinition Definition => new(
        Id: "resend-servicebus-dlq-to-queue",
        DisplayName: "Resend Service Bus DLQ to Queue",
        Description: "Resends dead-letter messages from a Service Bus DLQ back to the target queue.",
        RequiresConfirmation: true,
        Parameters:
        [
            new OperationParameter(
                Name: "sourceDlqPath",
                DisplayName: "Source DLQ Path",
                Description: "Dead-letter queue or subscription path to replay.",
                IsRequired: true,
                Type: "string",
                DefaultValue: null),
            new OperationParameter(
                Name: "queueName",
                DisplayName: "Target Queue",
                Description: "Queue that receives the replayed messages.",
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
        if (!context.Request.Input.TryGetValue("sourceDlqPath", out var sourceDlqPath) || string.IsNullOrWhiteSpace(sourceDlqPath))
        {
            errors.Add("The 'sourceDlqPath' parameter is required.");
        }

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
        var sourceDlqPath = context.Request.Input["sourceDlqPath"]!;
        var queueName = context.Request.Input["queueName"]!;
        var output = new Dictionary<string, string?>
        {
            ["sourceDlqPath"] = sourceDlqPath,
            ["queueName"] = queueName,
            ["executionMode"] = "stub",
            ["note"] = "Wire this operation to Azure Service Bus replay logic."
        };

        return Task.FromResult(
            new OperationExecutionResult(
                Status: OperationExecutionStatus.Succeeded,
                Message: $"Resend request accepted from '{sourceDlqPath}' to queue '{queueName}'.",
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                ErrorCode: null,
                Output: output));
    }
}
