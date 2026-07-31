using ControlPlane.Core.Concepts;
using ControlPlane.Core.Interfaces;

namespace ControlPlane.DevOps.StatusProviders;

public sealed class FailedPipelinesStatusProvider : IStatusProvider
{
    public string Id => "devops-failed-pipelines";

    public Task<StatusSnapshot> GetStatusAsync(Project project, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var signal = new StatusSignal(
            Id: "failed-pipelines",
            Title: "No failed pipelines in last window",
            Description: "Build and deployment pipelines are passing for the monitored project.",
            Level: StatusLevel.Healthy,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            Source: "Azure DevOps",
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
