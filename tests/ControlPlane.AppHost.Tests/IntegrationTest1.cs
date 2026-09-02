using System.Net;
using System.Net.Http.Json;

namespace ControlPlane.AppHost.Tests;

public sealed class ControlPlaneAppHostTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    [Fact(Timeout = 180_000)]
    public async Task ApiIsHealthyWhenStartedThroughAspire()
    {
        using var cancellationSource = new CancellationTokenSource(StartupTimeout);
        var cancellationToken = cancellationSource.Token;
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ControlPlane_AppHost>(cancellationToken);

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(StartupTimeout, cancellationToken);

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("api", cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);

        var endpoint = app.GetEndpoint("api", "https");
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var httpClient = new HttpClient(handler) { BaseAddress = endpoint };
        using var response = await httpClient.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(Timeout = 180_000)]
    public async Task ProjectEndpointReturnsNameConfiguredInEnvironment()
    {
        using var cancellationSource = new CancellationTokenSource(StartupTimeout);
        var cancellationToken = cancellationSource.Token;
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ControlPlane_AppHost>(cancellationToken);

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(StartupTimeout, cancellationToken);

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("api", cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken);

        var endpoint = app.GetEndpoint("api", "https");
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var httpClient = new HttpClient(handler) { BaseAddress = endpoint };

        var response = await httpClient.GetAsync("/api/projects/placeholder", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectDetailsResponse>(cancellationToken);
        Assert.NotNull(project);
        Assert.Equal("Control Plane", project.Project.Name);
    }

    private sealed record ProjectDetailsResponse(ProjectResponse Project);

    private sealed record ProjectResponse(string Id, string Name);
}
