namespace ControlPlane.Kafka;

public sealed class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>Comma-separated list of consumer group IDs to monitor for lag.</summary>
    public string MonitoredConsumerGroups { get; set; } = string.Empty;

    /// <summary>Comma-separated list of DLQ topic names to monitor.</summary>
    public string DlqTopics { get; set; } = string.Empty;

    public int ConsumerLagWarningThreshold { get; set; } = 1000;
}
