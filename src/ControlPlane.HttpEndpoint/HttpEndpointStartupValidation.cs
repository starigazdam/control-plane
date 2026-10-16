using Microsoft.Extensions.Hosting;

namespace ControlPlane.HttpEndpoint;

/// <summary>
/// Forces the endpoint configuration to load during host startup so a configured
/// missing, unreadable, or malformed file fails startup rather than the first request.
/// </summary>
internal sealed class HttpEndpointStartupValidation : IHostedService
{
    public HttpEndpointStartupValidation(HttpEndpointConfiguration configuration)
    {
        _ = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
