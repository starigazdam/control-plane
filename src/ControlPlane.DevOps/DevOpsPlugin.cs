using ControlPlane.Core.Plugins;
using ControlPlane.DevOps.Operations;
using ControlPlane.DevOps.StatusProviders;

namespace ControlPlane.DevOps;

public sealed class DevOpsPlugin : IControlPlanePlugin
{
    public string Name => "DevOps";

    public void Register(IPluginRegistration registration)
    {
        registration.AddStatusProvider<FailedPipelinesStatusProvider>();
        registration.AddOperation<RerunFailedPipelineOperation>();
    }
}
