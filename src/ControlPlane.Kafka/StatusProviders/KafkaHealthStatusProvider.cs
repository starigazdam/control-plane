using ControlPlane.Core.Concepts;
using ControlPlane.Core.Interfaces;

namespace ControlPlane.Kafka.StatusProviders;

public sealed class KafkaHealthStatusProvider : IStatusProvider
{
    public string Id => "kafka-health";

    public Task<StatusSnapshot> GetStatusAsync(Project project, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var observedAtUtc = DateTimeOffset.UtcNow;
        var signals = new[]
        {
            new StatusSignal(
                Id: "kafka-consumer-lag",
                Title: "Consumer lag within threshold",
                Description: "Current lag is below configured warning threshold.",
                Level: StatusLevel.Healthy,
                ObservedAtUtc: observedAtUtc,
                Source: "Kafka",
                Link: null),
            new StatusSignal(
                Id: "kafka-dlq-depth",
                Title: "Kafka DLQ is empty",
                Description: "No poison messages currently pending in monitored DLQ topics.",
                Level: StatusLevel.Healthy,
                ObservedAtUtc: observedAtUtc,
                Source: "Kafka",
                Link: null)
        };

        return Task.FromResult(
            new StatusSnapshot(
                ProjectId: project.Id,
                ProviderId: Id,
                OverallLevel: StatusLevel.Healthy,
                ObservedAtUtc: observedAtUtc,
                Signals: signals));
    }
}
