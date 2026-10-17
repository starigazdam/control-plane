using ControlPlane.Core.Plugins;
using ControlPlane.HttpEndpoint.Operations;
using ControlPlane.HttpEndpoint.StatusProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlPlane.HttpEndpoint;

/// <summary>
/// Registers configured manual HTTP endpoint checks with the Control Plane through the
/// existing plugin contracts. This slice provides manual <c>GET</c> and <c>HEAD</c>
/// checks only; schedules, cron parsing, and <c>POST</c> are out of scope.
/// </summary>
public sealed class HttpEndpointPlugin : IControlPlanePlugin, IPluginServiceRegistration
{
    public string Name => "HttpEndpoint";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(
            static serviceProvider => HttpEndpointConfigurationLoader.Load(
                serviceProvider.GetRequiredService<IOptions<HttpEndpointSettings>>().Value,
                serviceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath));

        services.AddSingleton<HttpEndpointChecker>();
        services.AddSingleton<HttpEndpointScanCoordinator>();

        // Resolve the configuration once during startup so a missing, unreadable, or
        // malformed file fails startup instead of failing on the first request.
        services.AddHostedService<HttpEndpointStartupValidation>();
    }

    public void Register(IPluginRegistration registration)
    {
        registration.AddStatusProvider<HttpEndpointStatusProvider>();
        registration.AddOperation<CheckHttpEndpointOperation>();
    }
}
