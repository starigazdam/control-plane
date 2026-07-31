using ControlPlane.Core.Concepts;
using ControlPlane.Core.Operations;

namespace ControlPlane.Api.Contracts;

public sealed record ProjectDetailsResponse(
    Project Project,
    StatusLevel StatusLevel,
    IReadOnlyList<StatusSnapshot> StatusSnapshots,
    IReadOnlyList<OperationDefinition> Operations);
