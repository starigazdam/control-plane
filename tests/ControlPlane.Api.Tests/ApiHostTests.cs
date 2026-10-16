using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.Api.Tests;

public sealed class ApiHostTests : IDisposable
{
    private const string ValidSha = "0123456789abcdef0123456789abcdef01234567";

    private static readonly string[] TrackedEnvironmentKeys =
    [
        "ASPNETCORE_ENVIRONMENT",
        "Database__Provider",
        "ConnectionStrings__ControlPlane",
        "Build__CommitSha",
        "ContainerHosting__Enabled"
    ];

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"control-plane-api-host-{Guid.NewGuid():N}.db");

    private readonly Dictionary<string, string?> _originalEnvironment = new(StringComparer.Ordinal);

    public ApiHostTests()
    {
        SetEnvironment("ASPNETCORE_ENVIRONMENT", "Production");
        SetEnvironment("Database__Provider", "Sqlite");
        SetEnvironment("ConnectionStrings__ControlPlane", $"Data Source={_databasePath}");
        SetEnvironment("Build__CommitSha", null);
        SetEnvironment("ContainerHosting__Enabled", null);
    }

    [Fact]
    public async Task Version_endpoint_reports_unknown_without_build_metadata()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Single(root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal("unknown", root.GetProperty("commitSha").GetString());
    }

    [Fact]
    public async Task Version_endpoint_reports_baked_commit_sha()
    {
        SetEnvironment("Build__CommitSha", ValidSha);

        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Single(root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(ValidSha, root.GetProperty("commitSha").GetString());
    }

    [Fact]
    public async Task Unknown_api_path_returns_404_without_the_ui_document()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/unknown");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Root_serves_compiled_ui_through_the_static_file_pipeline()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), $"control-plane-wwwroot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRoot);
        await File.WriteAllTextAsync(
            Path.Combine(webRoot, "index.html"),
            "<!doctype html><html><body>compiled control plane ui</body></html>");

        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseWebRoot(webRoot));
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("/");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("compiled control plane ui", body);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Container_hosting_enabled_serves_http_without_redirect()
    {
        SetEnvironment("ContainerHosting__Enabled", "true");

        using var factory = CreateFactoryWithConfiguredHttpsPort();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Absent_container_hosting_switch_keeps_https_redirection()
    {
        SetEnvironment("ContainerHosting__Enabled", null);

        using var factory = CreateFactoryWithConfiguredHttpsPort();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("http://localhost/api/version");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal(5001, response.Headers.Location.Port);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    [InlineData("/swagger/index.html")]
    public async Task Production_omits_development_only_endpoints(string path)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        foreach (var key in TrackedEnvironmentKeys)
        {
            Environment.SetEnvironmentVariable(
                key,
                _originalEnvironment.TryGetValue(key, out var value) ? value : null);
        }

        foreach (var suffix in new[] { string.Empty, "-shm", "-wal" })
        {
            var path = _databasePath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static WebApplicationFactory<Program> CreateFactoryWithConfiguredHttpsPort()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 5001)));
    }

    private void SetEnvironment(string key, string? value)
    {
        if (!_originalEnvironment.ContainsKey(key))
        {
            _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}
