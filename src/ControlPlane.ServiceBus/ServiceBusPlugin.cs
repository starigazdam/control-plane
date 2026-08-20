using ControlPlane.Core.Plugins;
using ControlPlane.ServiceBus.Operations;
using ControlPlane.ServiceBus.StatusProviders;

namespace ControlPlane.ServiceBus;

public sealed class ServiceBusPlugin : IControlPlanePlugin
{
    public string Name => "ServiceBus";

    public void Register(IPluginRegistration registration)
    {
        registration.AddStatusProvider<ServiceBusDlqStatusProvider>();
        registration.AddOperation<ResendServiceBusDlqToQueueOperation>();
        registration.AddOperation<ReplayServiceBusDlqOperation>();
        registration.AddOperation<PurgeServiceBusQueueOperation>();
    }
}
