using ControlPlane.Core.Plugins;
using ControlPlane.Azure.Operations;
using ControlPlane.Azure.StatusProviders;

namespace ControlPlane.Azure;

public sealed class AzurePlugin : IControlPlanePlugin
{
    public string Name => "Azure";

    public void Register(IPluginRegistration registration)
    {
        registration.AddStatusProvider<AzureResourceHealthStatusProvider>();
        registration.AddOperation<RestartAppServiceOperation>();
    }
}
