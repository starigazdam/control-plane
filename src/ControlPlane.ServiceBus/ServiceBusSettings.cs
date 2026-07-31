namespace ControlPlane.ServiceBus;

public sealed class ServiceBusSettings
{
    public const string SectionName = "ServiceBus";

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Comma-separated list of queue names to monitor for DLQ depth.</summary>
    public string MonitoredQueues { get; set; } = string.Empty;
}
