using ControlPlane.Core.Concepts;

namespace ControlPlane.Api.Contracts;

public sealed record OverviewProjectSummary(
    string ProjectId,
    string ProjectName,
    StatusLevel StatusLevel,
    int ActiveAlerts,
    int AvailableOperations);

public sealed record OverviewResponse(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<OverviewProjectSummary> Projects);
