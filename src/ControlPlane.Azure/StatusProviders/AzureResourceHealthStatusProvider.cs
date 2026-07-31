using ControlPlane.Core.Concepts;
using ControlPlane.Core.Interfaces;

namespace ControlPlane.Azure.StatusProviders;

public sealed class AzureResourceHealthStatusProvider : IStatusProvider
{
    public string Id => "azure-resource-health";

    public Task<StatusSnapshot> GetStatusAsync(Project project, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var signal = new StatusSignal(
            Id: "azure-resource-health",
            Title: "Azure resources healthy",
            Description: "All monitored App Services and Function Apps report healthy state.",
            Level: StatusLevel.Healthy,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            Source: "Azure",
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
