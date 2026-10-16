using System.Text.Json;
using ControlPlane.HttpEndpoint;
namespace ControlPlane.Api.Tests;
public sealed class HttpEndpointConfigurationTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"http-endpoint-cfg-{Guid.NewGuid():N}")).FullName;
    [Fact]
    public void Disabled_when_config_path_is_unset_or_empty()
    {
        var configuration = HttpEndpointConfigurationLoader.Load(new HttpEndpointSettings { ConfigPath = "   " }, _directory);
        Assert.False(configuration.IsEnabled);
        Assert.Empty(configuration.Endpoints);
        Assert.Empty(configuration.Warnings);
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("directory")]
    [InlineData("malformed")]
    [InlineData("nonarray")]
    [InlineData("toomany")]
    public void Invalid_source_throws_naming_setting_without_leaking_paths_or_parser_text(string kind)
    {
        var path = kind switch
        {
            "missing" => Path.Combine(_directory, "does-not-exist.json"),
            "directory" => Directory.CreateDirectory(Path.Combine(_directory, $"unreadable-{Guid.NewGuid():N}")).FullName,
            "malformed" => WriteConfig("{ not json ]"),
            "nonarray" => WriteConfig("{\"id\":\"one\"}"),
            _ => WriteConfig(JsonSerializer.Serialize(Enumerable.Range(0, 21).Select(index => ValidEntry($"endpoint-{index}"))))
        };
        var exception = Assert.Throws<HttpEndpointConfigurationException>(() =>
            HttpEndpointConfigurationLoader.Load(new HttpEndpointSettings { ConfigPath = path }, _directory));
        Assert.Contains("HttpEndpoint__ConfigPath", exception.Message);
        Assert.Contains(path, exception.Message);
        Assert.DoesNotContain("http://", exception.Message);
        Assert.DoesNotContain("https://", exception.Message);
        Assert.DoesNotContain("not json", exception.Message);
        Assert.DoesNotContain("UnauthorizedAccessException", exception.Message);
        Assert.DoesNotContain("IOException", exception.Message);
        Assert.DoesNotContain("Access to the path", exception.Message);
    }
    [Fact]
    public void Resolves_relative_paths_from_content_root()
    {
        File.WriteAllText(Path.Combine(_directory, "relative.json"), JsonSerializer.Serialize(new[] { ValidEntry("alpha") }));
        var configuration = HttpEndpointConfigurationLoader.Load(new HttpEndpointSettings { ConfigPath = "relative.json" }, _directory);
        Assert.True(configuration.IsEnabled);
        Assert.Equal("alpha", Assert.Single(configuration.Endpoints).Id);
    }
    [Fact]
    public void Excludes_invalid_entry_and_reports_index_with_safe_warning()
    {
        var configuration = Load(JsonSerializer.Serialize(new object[]
        {
            ValidEntry("good"),
            new { id = "bad", name = "Bad", url = "not-a-url", expectedStatus = 200, method = "GET" }
        }));
        Assert.Equal("good", Assert.Single(configuration.Endpoints).Id);
        var warning = Assert.Single(configuration.Warnings);
        Assert.Equal(1, warning.Index);
        Assert.Equal("invalid_url", warning.Code);
    }
    [Fact]
    public void Excludes_every_definition_in_a_duplicate_id_group()
    {
        var configuration = Load(JsonSerializer.Serialize(new[] { ValidEntry("Alpha"), ValidEntry("alpha"), ValidEntry("Beta") }));
        Assert.Equal("Beta", Assert.Single(configuration.Endpoints).Id);
        Assert.Equal([0, 1], configuration.Warnings.Select(warning => warning.Index).ToArray());
        Assert.All(configuration.Warnings, warning => Assert.Equal("duplicate_id", warning.Code));
    }
    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("has/slash")]
    public void Rejects_invalid_ids(string id)
    {
        var configuration = Load(JsonSerializer.Serialize(new[] { Entry(id) }));
        Assert.Empty(configuration.Endpoints);
        Assert.Equal("invalid_id", Assert.Single(configuration.Warnings).Code);
    }
    [Fact]
    public void Rejects_id_longer_than_sixty_four_characters()
    {
        var configuration = Load(JsonSerializer.Serialize(new[] { Entry(new string('a', 65)) }));
        Assert.Empty(configuration.Endpoints);
        Assert.Equal("invalid_id", Assert.Single(configuration.Warnings).Code);
    }
    [Theory]
    [InlineData("ftp://localhost/health")]
    [InlineData("http://user:pass@localhost/health")]
    [InlineData("relative/path")]
    public void Rejects_unsupported_urls(string url)
    {
        var configuration = Load(JsonSerializer.Serialize(new[] { Entry("one", url: url) }));
        Assert.Empty(configuration.Endpoints);
        Assert.Equal("invalid_url", Assert.Single(configuration.Warnings).Code);
    }
    [Fact]
    public void Rejects_unsupported_method()
    {
        var configuration = Load(JsonSerializer.Serialize(new[] { Entry("one", method: "POST") }));
        Assert.Empty(configuration.Endpoints);
        Assert.Equal("invalid_method", Assert.Single(configuration.Warnings).Code);
    }
    [Theory]
    [InlineData(99)]
    [InlineData(600)]
    public void Rejects_expected_status_out_of_bounds(int status)
    {
        var configuration = Load(JsonSerializer.Serialize(new[] { Entry("one", expectedStatus: status) }));
        Assert.Empty(configuration.Endpoints);
        Assert.Equal("invalid_status", Assert.Single(configuration.Warnings).Code);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Rejects_timeout_out_of_bounds(int timeout)
    {
        var configuration = Load(JsonSerializer.Serialize(new[] { TimedEntry("one", timeout) }));
        Assert.Empty(configuration.Endpoints);
        Assert.Equal("invalid_timeout", Assert.Single(configuration.Warnings).Code);
    }
    [Fact]
    public void Defaults_timeout_to_five_seconds_and_accepts_one_to_ten()
    {
        var configuration = Load(JsonSerializer.Serialize(new object[]
        {
            ValidEntry("default"),
            TimedEntry("min", 1),
            TimedEntry("max", 10)
        }));
        Assert.Equal(TimeSpan.FromSeconds(5), configuration.FindById("default")!.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(1), configuration.FindById("min")!.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(10), configuration.FindById("max")!.Timeout);
    }
    [Fact]
    public void Schedule_fields_surface_one_plugin_warning_without_starting_a_schedule()
    {
        var configuration = Load(
            JsonSerializer.Serialize(new object[]
            {
                ValidEntry("one"),
                new { id = "two", name = "Two", url = "http://localhost/two", expectedStatus = 200, method = "GET", cron = "0 * * * *" }
            }),
            defaultCron: "*/10 * * * *");
        Assert.True(configuration.HasScheduleWarning);
        Assert.Equal(2, configuration.Endpoints.Count);
        Assert.Empty(configuration.Warnings);
    }
    private HttpEndpointConfiguration Load(string json, string? defaultCron = null)
        => HttpEndpointConfigurationLoader.Load(
            new HttpEndpointSettings { ConfigPath = WriteConfig(json), DefaultCron = defaultCron ?? string.Empty },
            _directory);
    private string WriteConfig(string json)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
    private static object ValidEntry(string id) => new
    {
        id,
        name = $"Endpoint {id}",
        url = "http://localhost/health",
        expectedStatus = 200,
        method = "GET"
    };
    private static object Entry(string id, string url = "http://localhost/health", int expectedStatus = 200, string method = "GET")
        => new { id, name = "Name", url, expectedStatus, method };
    private static object TimedEntry(string id, int timeoutSeconds)
        => new { id, name = "Name", url = "http://localhost/health", expectedStatus = 200, method = "GET", timeoutSeconds };
    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); } catch (DirectoryNotFoundException) { }
    }
}
