using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.ExternalPlugins;

/// <summary>
/// Bridges separately deployed external plugins into the host. It reads the static
/// <c>ExternalPlugins</c> configuration list, discovers capabilities once at
/// startup, and registers one <c>{name}.bridge</c> status provider per plugin plus
/// one status provider or operation per remote capability.
/// </summary>
public sealed class ExternalPluginsPlugin : IControlPlanePlugin, IPluginServiceRegistration
{
    private readonly TimeSpan _timeout;
    private IReadOnlyList<ExternalPluginDiscovery> _discoveries = [];

    public ExternalPluginsPlugin()
        : this(ExternalPluginsHttpClient.DefaultTimeout)
    {
    }

    internal ExternalPluginsPlugin(TimeSpan timeout)
    {
        _timeout = timeout;
    }

    public string Name => "ExternalPlugins";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        var definitions = ExternalPluginConfiguration.Parse(configuration);
        if (definitions.Count == 0)
        {
            return;
        }

        ExternalPluginsHttpClient.Register(services);

        using var client = ExternalPluginsHttpClient.CreateStartupClient();
        var discoveries = new List<ExternalPluginDiscovery>(definitions.Count);
        foreach (var definition in definitions)
        {
            discoveries.Add(ExternalPluginDiscoveryClient.Discover(client, definition, _timeout));
        }

        _discoveries = discoveries;
    }

    public void Register(IPluginRegistration registration)
    {
        foreach (var discovery in _discoveries)
        {
            registration.AddStatusProvider(new ExternalBridgeStatusProvider(discovery));

            if (discovery.Status != ExternalDiscoveryStatus.Succeeded)
            {
                continue;
            }

            foreach (var remoteId in discovery.StatusProviderIds)
            {
                registration.AddStatusProvider(serviceProvider => new ExternalRemoteStatusProvider(
                    discovery.Name,
                    remoteId,
                    discovery.BaseUri,
                    serviceProvider.GetRequiredService<IHttpClientFactory>(),
                    _timeout));
            }

            foreach (var definition in discovery.Operations)
            {
                registration.AddOperation(serviceProvider => new ExternalRemoteOperation(
                    discovery.Name,
                    definition,
                    discovery.BaseUri,
                    serviceProvider.GetRequiredService<IHttpClientFactory>(),
                    _timeout));
            }
        }
    }
}
