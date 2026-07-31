namespace ControlPlane.Core.Concepts;

public sealed record StatusSignal(
    string Id,
    string Title,
    string Description,
    StatusLevel Level,
    DateTimeOffset ObservedAtUtc,
    string? Source,
    string? Link);
