using Microsoft.AspNetCore.Mvc.Testing;
namespace ControlPlane.Api.E2E;
/// <summary>Configured but missing endpoint file must fail startup naming the setting and path.</summary>
public sealed class HttpEndpointStartupTests
{
    [Fact]
    public async Task Startup_fails_when_configured_endpoint_file_is_missing()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"http-endpoint-missing-{Guid.NewGuid():N}.json");
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var previousConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ControlPlane");
        var previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var previousConfigPath = Environment.GetEnvironmentVariable("HttpEndpoint__ConfigPath");
        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__ControlPlane", $"Data Source={tempDbPath}");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("HttpEndpoint__ConfigPath", missingPath);
            await using var factory = new WebApplicationFactory<Program>();
            var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.Contains("HttpEndpoint__ConfigPath", exception.ToString(), StringComparison.Ordinal);
            Assert.Contains(missingPath, exception.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("http://", exception.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__ControlPlane", previousConnectionString);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
            Environment.SetEnvironmentVariable("HttpEndpoint__ConfigPath", previousConfigPath);
            foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
            {
                var path = tempDbPath + suffix;
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
