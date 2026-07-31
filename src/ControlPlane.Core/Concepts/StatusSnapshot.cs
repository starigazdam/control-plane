namespace ControlPlane.Core.Concepts;

public sealed record StatusSnapshot(
    string ProjectId,
    string ProviderId,
    StatusLevel OverallLevel,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<StatusSignal> Signals);
