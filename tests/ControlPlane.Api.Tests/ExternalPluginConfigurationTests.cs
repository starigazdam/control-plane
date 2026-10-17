using ControlPlane.ExternalPlugins;
using Microsoft.Extensions.Configuration;

namespace ControlPlane.Api.Tests;

public sealed class ExternalPluginConfigurationTests
{
    [Fact]
    public void Empty_configuration_registers_no_plugins()
    {
        Assert.Empty(ExternalPluginConfiguration.Parse(Build()));
    }

    [Fact]
    public void Valid_entry_yields_a_definition()
    {
        var definitions = ExternalPluginConfiguration.Parse(Build(
            ("ExternalPlugins:Plugins:0:Name", "sample"),
            ("ExternalPlugins:Plugins:0:BaseUrl", "http://localhost:5099/plugins")));

        var definition = Assert.Single(definitions);
        Assert.Equal("sample", definition.Name);
        Assert.Equal("http://localhost:5099/plugins", definition.BaseUri.AbsoluteUri.TrimEnd('/'));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sample")]
    [InlineData("1sample")]
    [InlineData("-sample")]
    [InlineData("sample_plugin")]
    [InlineData("this-name-is-definitely-way-too-long")]
    public void Invalid_name_fails_startup_and_names_the_setting(string name)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExternalPluginConfiguration.Parse(Build(
                ("ExternalPlugins:Plugins:0:Name", name),
                ("ExternalPlugins:Plugins:0:BaseUrl", "http://localhost:5099"))));

        Assert.Contains("ExternalPlugins:Plugins[0]:Name", exception.Message);
    }

    [Fact]
    public void Duplicate_name_fails_startup()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExternalPluginConfiguration.Parse(Build(
                ("ExternalPlugins:Plugins:0:Name", "sample"),
                ("ExternalPlugins:Plugins:0:BaseUrl", "http://localhost:5099"),
                ("ExternalPlugins:Plugins:1:Name", "sample"),
                ("ExternalPlugins:Plugins:1:BaseUrl", "http://localhost:5100"))));

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("localhost:5099")]
    [InlineData("ftp://localhost:5099")]
    [InlineData("http://localhost:5099?x=1")]
    [InlineData("http://localhost:5099#fragment")]
    [InlineData("http://user:pass@localhost:5099")]
    public void Invalid_base_url_fails_startup_and_names_the_setting(string baseUrl)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExternalPluginConfiguration.Parse(Build(
                ("ExternalPlugins:Plugins:0:Name", "sample"),
                ("ExternalPlugins:Plugins:0:BaseUrl", baseUrl))));

        Assert.Contains("ExternalPlugins:Plugins[0]:BaseUrl", exception.Message);
    }

    private static IConfiguration Build(params (string Key, string Value)[] values)
    {
        var pairs = values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value));
        return new ConfigurationBuilder().AddInMemoryCollection(pairs).Build();
    }
}
