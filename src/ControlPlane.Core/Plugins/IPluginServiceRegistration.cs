using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.Core.Plugins;

/// <summary>
/// Extends <see cref="IPluginRegistration"/> with a delegate for registering
/// plugin-owned services into the DI container.
/// Plugins that need constructor-injected services (HTTP clients, CLI runners, etc.)
/// implement this interface alongside <see cref="IControlPlanePlugin"/> to wire up
/// their own service registrations before status providers and operations are added.
/// </summary>
public interface IPluginServiceRegistration
{
    /// <summary>
    /// Called during application startup to register plugin-specific services. The host
    /// passes the application configuration so a plugin can bind its own section and
    /// register named clients before <see cref="IControlPlanePlugin.Register"/> runs.
    /// </summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);
}
