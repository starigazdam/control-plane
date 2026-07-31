namespace ControlPlane.Core.Plugins;

public interface IControlPlanePlugin
{
    string Name { get; }

    void Register(IPluginRegistration registration);
}
