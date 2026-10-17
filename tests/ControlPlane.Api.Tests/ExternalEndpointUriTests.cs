using ControlPlane.ExternalPlugins;

namespace ControlPlane.Api.Tests;

public sealed class ExternalEndpointUriTests
{
    [Theory]
    [InlineData("http://localhost:5099")]
    [InlineData("http://localhost:5099/")]
    public void Capabilities_preserves_the_base_path_with_one_slash(string baseUrl)
    {
        var uri = ExternalEndpointUri.Capabilities(new Uri(baseUrl));
        Assert.Equal("http://localhost:5099/capabilities", uri.AbsoluteUri);
    }

    [Fact]
    public void Base_url_with_a_path_is_preserved()
    {
        var uri = ExternalEndpointUri.Execute(new Uri("http://localhost:5099/plugins"), "ping");
        Assert.Equal("http://localhost:5099/plugins/operations/ping/execute", uri.AbsoluteUri);
    }

    [Fact]
    public void Project_id_is_percent_encoded()
    {
        var uri = ExternalEndpointUri.Status(new Uri("http://localhost:5099"), "health", "pro ject/one");
        Assert.Contains("projectId=pro%20ject%2Fone", uri.Query);
    }

    [Fact]
    public void Remote_id_is_percent_encoded_as_one_path_segment()
    {
        var uri = ExternalEndpointUri.Validate(new Uri("http://localhost:5099"), "a/b");
        Assert.Contains("operations/a%2Fb/validate", uri.AbsoluteUri);
    }
}
