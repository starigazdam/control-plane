using ControlPlane.Core.Concepts;

namespace ControlPlane.Core.Interfaces;

public interface IStatusProvider
{
    string Id { get; }

    Task<StatusSnapshot> GetStatusAsync(Project project, CancellationToken cancellationToken);
}
