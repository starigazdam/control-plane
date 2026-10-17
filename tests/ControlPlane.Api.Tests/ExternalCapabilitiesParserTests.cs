using ControlPlane.ExternalPlugins;

namespace ControlPlane.Api.Tests;

public sealed class ExternalCapabilitiesParserTests
{
    [Fact]
    public void Valid_capabilities_are_parsed_with_defaults()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            {
              "statusProviderIds": ["health"],
              "operations": [ { "id": "ping", "displayName": "Ping" } ]
            }
            """);

        Assert.True(result.IsValid);
        Assert.Equal(["health"], result.StatusProviderIds);
        var operation = Assert.Single(result.Operations);
        Assert.Equal("ping", operation.RemoteId);
        Assert.Equal("Ping", operation.DisplayName);
        Assert.Equal(string.Empty, operation.Description);
        Assert.False(operation.RequiresConfirmation);
        Assert.Empty(operation.Parameters);
        Assert.Empty(result.Exclusions);
    }

    [Fact]
    public void Parameter_defaults_apply_when_optional_fields_are_null()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            {
              "statusProviderIds": [],
              "operations": [
                {
                  "id": "restart",
                  "displayName": "Restart",
                  "description": null,
                  "requiresConfirmation": null,
                  "parameters": [
                    { "name": "service", "type": "string", "displayName": null, "description": null, "isRequired": null, "defaultValue": null }
                  ]
                }
              ]
            }
            """);

        var operation = Assert.Single(result.Operations);
        var parameter = Assert.Single(operation.Parameters);
        Assert.Equal("service", parameter.Name);
        Assert.Equal("service", parameter.DisplayName);
        Assert.Equal(string.Empty, parameter.Description);
        Assert.False(parameter.IsRequired);
        Assert.Null(parameter.DefaultValue);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"statusProviderIds\":[]}")]
    [InlineData("{\"operations\":[]}")]
    [InlineData("{\"statusProviderIds\":\"health\",\"operations\":[]}")]
    public void Invalid_top_level_shape_is_invalid_capabilities(string body)
    {
        Assert.False(ExternalCapabilitiesParser.Parse(body).IsValid);
    }

    [Theory]
    [InlineData("health", true)]
    [InlineData("Health.v1", true)]
    [InlineData("a_b-c9", true)]
    [InlineData("0abc", true)]
    [InlineData("", false)]
    [InlineData("-abc", false)]
    [InlineData(".abc", false)]
    [InlineData("bad id", false)]
    [InlineData("bad!", false)]
    public void Remote_id_grammar(string id, bool expected)
    {
        Assert.Equal(expected, ExternalCapabilitiesParser.IsValidRemoteId(id));
    }

    [Fact]
    public void Non_string_status_provider_element_is_excluded()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            { "statusProviderIds": ["health", 42], "operations": [] }
            """);

        Assert.Equal(["health"], result.StatusProviderIds);
        Assert.Contains(result.Exclusions, exclusion =>
            exclusion.Kind == "status-provider" && exclusion.Identifier == "#1");
    }

    [Theory]
    [InlineData("bridge")]
    [InlineData("BRIDGE")]
    public void Reserved_bridge_status_provider_is_excluded(string id)
    {
        var result = ExternalCapabilitiesParser.Parse($$"""
            { "statusProviderIds": ["{{id}}"], "operations": [] }
            """);

        Assert.Empty(result.StatusProviderIds);
        Assert.Contains(result.Exclusions, exclusion =>
            exclusion.Kind == "status-provider" && exclusion.Identifier == id);
    }

    [Fact]
    public void Invalid_status_provider_id_is_excluded()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            { "statusProviderIds": ["bad id", "health"], "operations": [] }
            """);

        Assert.Equal(["health"], result.StatusProviderIds);
        Assert.Contains(result.Exclusions, exclusion => exclusion.Identifier == "bad id");
    }

    [Fact]
    public void Duplicate_status_provider_ids_exclude_every_entry_case_insensitively()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            { "statusProviderIds": ["dup", "DUP", "health"], "operations": [] }
            """);

        Assert.Equal(["health"], result.StatusProviderIds);
        Assert.Contains(result.Exclusions, exclusion => exclusion.Identifier == "dup");
        Assert.Contains(result.Exclusions, exclusion => exclusion.Identifier == "DUP");
    }

    [Fact]
    public void Duplicate_operation_ids_exclude_every_entry()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            {
              "statusProviderIds": [],
              "operations": [
                { "id": "ping", "displayName": "Ping A" },
                { "id": "PING", "displayName": "Ping B" },
                { "id": "restart", "displayName": "Restart" }
              ]
            }
            """);

        var operation = Assert.Single(result.Operations);
        Assert.Equal("restart", operation.RemoteId);
        Assert.Contains(result.Exclusions, exclusion => exclusion.Identifier == "ping");
        Assert.Contains(result.Exclusions, exclusion => exclusion.Identifier == "PING");
    }

    [Fact]
    public void Operation_with_invalid_grammar_id_is_excluded()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            {
              "statusProviderIds": [],
              "operations": [ { "id": "bad id", "displayName": "Bad" } ]
            }
            """);

        Assert.Empty(result.Operations);
        Assert.Contains(result.Exclusions, exclusion =>
            exclusion.Kind == "operation" && exclusion.Identifier == "bad id");
    }

    [Fact]
    public void Operation_missing_a_required_field_is_excluded_but_others_register()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            {
              "statusProviderIds": [],
              "operations": [
                { "id": "ping" },
                { "id": "restart", "displayName": "Restart" }
              ]
            }
            """);

        Assert.Equal("restart", Assert.Single(result.Operations).RemoteId);
        Assert.Contains(result.Exclusions, exclusion => exclusion.Identifier == "ping");
    }

    [Fact]
    public void Operation_with_wrongly_typed_optional_field_is_excluded()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            {
              "statusProviderIds": [],
              "operations": [ { "id": "ping", "displayName": "Ping", "requiresConfirmation": "yes" } ]
            }
            """);

        Assert.Empty(result.Operations);
        Assert.Contains(result.Exclusions, exclusion => exclusion.Identifier == "ping");
    }

    [Fact]
    public void Operation_with_non_array_parameters_is_excluded()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            {
              "statusProviderIds": [],
              "operations": [ { "id": "ping", "displayName": "Ping", "parameters": "none" } ]
            }
            """);

        Assert.Empty(result.Operations);
        Assert.Contains(result.Exclusions, exclusion => exclusion.Identifier == "ping");
    }

    [Fact]
    public void Operation_with_invalid_parameter_is_excluded()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            {
              "statusProviderIds": [],
              "operations": [
                { "id": "ping", "displayName": "Ping", "parameters": [ { "name": "endpoint" } ] }
              ]
            }
            """);

        Assert.Empty(result.Operations);
        Assert.Contains(result.Exclusions, exclusion => exclusion.Kind == "operation");
    }

    [Fact]
    public void Operation_with_non_string_default_value_is_excluded()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            {
              "statusProviderIds": [],
              "operations": [
                { "id": "ping", "displayName": "Ping", "parameters": [ { "name": "count", "type": "number", "defaultValue": 3 } ] }
              ]
            }
            """);

        Assert.Empty(result.Operations);
        Assert.Contains(result.Exclusions, exclusion => exclusion.Kind == "operation");
    }

    [Fact]
    public void Operation_element_that_is_not_an_object_is_excluded()
    {
        var result = ExternalCapabilitiesParser.Parse("""
            { "statusProviderIds": [], "operations": [ 42, { "id": "ping", "displayName": "Ping" } ] }
            """);

        Assert.Equal("ping", Assert.Single(result.Operations).RemoteId);
        Assert.Contains(result.Exclusions, exclusion =>
            exclusion.Kind == "operation" && exclusion.Identifier == "#0");
    }
}
