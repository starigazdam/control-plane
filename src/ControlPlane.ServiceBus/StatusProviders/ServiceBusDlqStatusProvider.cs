using ControlPlane.Core.Concepts;
using ControlPlane.Core.Interfaces;

namespace ControlPlane.ServiceBus.StatusProviders;

public sealed class ServiceBusDlqStatusProvider : IStatusProvider
{
    public string Id => "servicebus-dlq";

    public Task<StatusSnapshot> GetStatusAsync(Project project, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var signal = new StatusSignal(
            Id: "servicebus-dlq-depth",
            Title: "Service Bus DLQ depth is low",
            Description: "Monitored queues have no significant dead-letter backlog.",
            Level: StatusLevel.Healthy,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            Source: "Service Bus",
            Link: null);

        return Task.FromResult(
            new StatusSnapshot(
                ProjectId: project.Id,
                ProviderId: Id,
                OverallLevel: StatusLevel.Healthy,
                ObservedAtUtc: DateTimeOffset.UtcNow,
                Signals: [signal]));
    }
}
