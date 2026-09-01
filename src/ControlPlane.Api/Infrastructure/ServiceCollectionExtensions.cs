using System.Reflection;
using ControlPlane.Azure;
using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Plugins;
using ControlPlane.CopilotAgent;
using ControlPlane.DevOps;
using ControlPlane.Kafka;
using ControlPlane.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddControlPlanePlugins(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
    {
        // ── Settings ─────────────────────────────────────────────────────────
        services.Configure<AzureSettings>(configuration.GetSection(AzureSettings.SectionName));
        services.Configure<ServiceBusSettings>(configuration.GetSection(ServiceBusSettings.SectionName));
        services.Configure<KafkaSettings>(configuration.GetSection(KafkaSettings.SectionName));
        services.Configure<DevOpsSettings>(configuration.GetSection(DevOpsSettings.SectionName));
        services.Configure<CopilotAgentSettings>(configuration.GetSection(CopilotAgentSettings.SectionName));

        // ── Plugin discovery ─────────────────────────────────────────────────
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

            // Allow plugins to register their own DI services (e.g. ICopilotAgentRunner)
            // before status providers and operations are wired up.
            if (plugin is IPluginServiceRegistration serviceRegistration)
            {
                serviceRegistration.RegisterServices(services);
            }

            plugin.Register(registration);
        }

        // ── Status providers and operations ───────────────────────────────────
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
