namespace ControlPlane.Api.Persistence;

public sealed class OperationExecutionEntity
{
    public long Id { get; set; }

    public string ProjectId { get; set; } = string.Empty;

    public string OperationId { get; set; } = string.Empty;

    public string InitiatedBy { get; set; } = string.Empty;

    public DateTimeOffset RequestedAtUtc { get; set; }

    public string? CorrelationId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset CompletedAtUtc { get; set; }

    public string? ErrorCode { get; set; }

    public string InputJson { get; set; } = "{}";

    public string OutputJson { get; set; } = "{}";
}
