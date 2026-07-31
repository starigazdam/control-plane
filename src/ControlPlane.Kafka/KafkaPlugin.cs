using ControlPlane.Core.Plugins;
using ControlPlane.Kafka.Operations;
using ControlPlane.Kafka.StatusProviders;

namespace ControlPlane.Kafka;

public sealed class KafkaPlugin : IControlPlanePlugin
{
    public string Name => "Kafka";

    public void Register(IPluginRegistration registration)
    {
        registration.AddStatusProvider<KafkaHealthStatusProvider>();
        registration.AddOperation<ReplayKafkaDlqOperation>();
    }
}
