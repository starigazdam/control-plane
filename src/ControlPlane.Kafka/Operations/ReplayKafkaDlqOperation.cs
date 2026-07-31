using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Operations;

namespace ControlPlane.Kafka.Operations;

public sealed class ReplayKafkaDlqOperation : IOperation
{
    public OperationDefinition Definition => new(
        Id: "replay-kafka-dlq",
        DisplayName: "Replay Kafka DLQ",
        Description: "Replays Kafka dead-letter topic messages to the primary processing topic.",
        RequiresConfirmation: true,
        Parameters:
        [
            new OperationParameter(
                Name: "topic",
                DisplayName: "DLQ Topic",
                Description: "Dead-letter topic to replay.",
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
        if (!context.Request.Input.TryGetValue("topic", out var topic) || string.IsNullOrWhiteSpace(topic))
        {
            errors.Add("The 'topic' parameter is required.");
        }

        return Task.FromResult<IReadOnlyList<string>>(errors);
    }

    public Task<OperationExecutionResult> ExecuteAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startedAtUtc = DateTimeOffset.UtcNow;
        var topic = context.Request.Input["topic"]!;
        var output = new Dictionary<string, string?>
        {
            ["topic"] = topic,
            ["executionMode"] = "stub",
            ["note"] = "Wire this operation to Kafka client replay implementation."
        };

        return Task.FromResult(
            new OperationExecutionResult(
                Status: OperationExecutionStatus.Succeeded,
                Message: $"Replay request accepted for Kafka DLQ topic '{topic}'.",
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                ErrorCode: null,
                Output: output));
    }
}
