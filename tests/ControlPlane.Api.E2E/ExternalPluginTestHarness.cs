using Microsoft.AspNetCore.Mvc.Testing;

namespace ControlPlane.Api.E2E;

/// <summary>
/// Sets the external plugin configuration through process environment variables
/// before the host is built, points the host at a throwaway SQLite database, and
/// restores every variable it touched when disposed. Test classes are serialized,
/// so process-wide environment mutation is safe here.
/// </summary>
internal sealed class ExternalPluginTestHarness : IAsyncDisposable
{
    private readonly Dictionary<string, string?> _original = new(StringComparer.Ordinal);
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
    private WebApplicationFactory<Program>? _factory;

    public void SetPlugin(int index, string name, string baseUrl)
    {
        Set($"ExternalPlugins__Plugins__{index}__Name", name);
        Set($"ExternalPlugins__Plugins__{index}__BaseUrl", baseUrl);
    }

    public void Set(string key, string? value)
    {
        if (!_original.ContainsKey(key))
        {
            _original[key] = Environment.GetEnvironmentVariable(key);
        }

        Environment.SetEnvironmentVariable(key, value);
    }

    public WebApplicationFactory<Program> CreateFactory()
    {
        Set("ConnectionStrings__ControlPlane", $"Data Source={_databasePath}");
        Set("ASPNETCORE_ENVIRONMENT", "Development");
        _factory = new WebApplicationFactory<Program>();
        return _factory;
    }

    public HttpClient CreateClient() => CreateFactory().CreateClient();

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
            _factory = null;
        }

        foreach (var (key, value) in _original)
        {
            Environment.SetEnvironmentVariable(key, value);
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
}
