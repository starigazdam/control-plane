using System.Net;

namespace ControlPlane.AppHost.Tests.Tests;

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

        using var httpClient = app.CreateHttpClient("api");
        using var response = await httpClient.GetAsync("/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
