using System.Reflection;
using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Plugins;

namespace ControlPlane.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddControlPlanePlugins(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        var pluginTypes = assemblies
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false } &&
                typeof(IControlPlanePlugin).IsAssignableFrom(type))
            .Select(type => type.AsType())
            .ToArray();

        var registration = new PluginRegistration();
        var pluginNames = new List<string>(pluginTypes.Length);

        foreach (var pluginType in pluginTypes)
        {
            if (Activator.CreateInstance(pluginType) is not IControlPlanePlugin plugin)
            {
                throw new InvalidOperationException(
                    $"Failed to instantiate plugin type '{pluginType.FullName}'.");
            }

            pluginNames.Add(plugin.Name);
            plugin.Register(registration);
        }

        foreach (var providerType in registration.StatusProviderTypes)
        {
            services.AddTransient(typeof(IStatusProvider), providerType);
        }

        foreach (var operationType in registration.OperationTypes)
        {
            services.AddTransient(typeof(IOperation), operationType);
        }

        services.AddSingleton(
            new DiscoveredPluginCatalog(
                pluginNames.AsReadOnly(),
                registration.StatusProviderTypes,
                registration.OperationTypes));

        return services;
    }
}
