using System.Reflection;
using ControlPlane.Azure;
using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Plugins;
using ControlPlane.CopilotAgent;
using ControlPlane.DevOps;
using ControlPlane.HttpEndpoint;
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
        services.Configure<HttpEndpointSettings>(configuration.GetSection(HttpEndpointSettings.SectionName));

        // ── HTTP endpoint plugin client ──────────────────────────────────────
        // One dedicated client for endpoint checks. Override the shared
        // ServiceDefaults defaults: no resilience (retries, circuit breaker, or a
        // second timeout layer), no service-discovery resolution, no automatic
        // redirects, no framework request logs, and no URL-bearing spans. The
        // per-entry timeout is applied by the checker.
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is experimental but is the supported opt-out.
        var httpEndpointClient = services
            .AddHttpClient(HttpEndpointChecker.HttpClientName)
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        httpEndpointClient
            .RemoveAllLoggers()
            .ConfigureHttpClient(static client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigureAdditionalHttpMessageHandlers(static (handlers, _) =>
            {
                for (var index = handlers.Count - 1; index >= 0; index--)
                {
                    if (string.Equals(
                            handlers[index].GetType().FullName,
                            "Microsoft.Extensions.ServiceDiscovery.Http.ResolvingHttpDelegatingHandler",
                            StringComparison.Ordinal))
                    {
                        handlers.RemoveAt(index);
                    }
                }
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            });

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
                serviceRegistration.RegisterServices(services, configuration);
            }

            plugin.Register(registration);
        }

        // ── Status providers and operations ───────────────────────────────────
        foreach (var descriptor in registration.StatusProviders)
        {
            RegisterCapability(services, descriptor);
        }

        foreach (var descriptor in registration.Operations)
        {
            RegisterCapability(services, descriptor);
        }

        services.AddSingleton(
            new DiscoveredPluginCatalog(
                pluginNames.AsReadOnly(),
                registration.StatusProviders,
                registration.Operations));

        return services;
    }

    private static void RegisterCapability(IServiceCollection services, PluginCapabilityDescriptor descriptor)
    {
        if (descriptor.ImplementationType is { } implementationType)
        {
            services.AddTransient(descriptor.ContractType, implementationType);
        }
        else if (descriptor.Instance is { } instance)
        {
            services.AddSingleton(descriptor.ContractType, instance);
        }
        else if (descriptor.Factory is { } factory)
        {
            services.AddSingleton(descriptor.ContractType, serviceProvider => factory(serviceProvider));
        }
        else
        {
            throw new InvalidOperationException(
                $"Plugin capability descriptor for contract '{descriptor.ContractType.FullName}' is empty.");
        }
    }
}
