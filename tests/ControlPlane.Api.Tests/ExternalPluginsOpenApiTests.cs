using System.Text.Json;

namespace ControlPlane.Api.Tests;

/// <summary>
/// Checks that each fixture response shape matches the published OpenAPI contract
/// without a validator package: required properties are present and carry the
/// declared JSON types.
/// </summary>
public sealed class ExternalPluginsOpenApiTests
{
    private static readonly JsonDocument OpenApi = LoadOpenApi();

    public static IEnumerable<object[]> FixtureResponses()
    {
        yield return
        [
            "Capabilities",
            """
            {
              "statusProviderIds": ["health"],
              "operations": [
                { "id": "ping", "displayName": "Ping", "description": "Returns a pong result.", "requiresConfirmation": false, "parameters": [] },
                {
                  "id": "restart",
                  "displayName": "Restart",
                  "description": "Restarts a named service.",
                  "requiresConfirmation": true,
                  "parameters": [
                    { "name": "service", "displayName": "Service", "description": "Service to restart.", "isRequired": true, "type": "string", "defaultValue": null }
                  ]
                }
              ]
            }
            """
        ];

        yield return
        [
            "StatusSnapshot",
            """
            {
              "projectId": "placeholder",
              "providerId": "health",
              "overallLevel": "Healthy",
              "observedAtUtc": "2026-01-01T00:00:00+00:00",
              "signals": [
                { "id": "ok", "title": "Healthy", "description": "The fabricated plugin is healthy.", "level": "Healthy", "observedAtUtc": "2026-01-01T00:00:00+00:00", "source": null, "link": null }
              ]
            }
            """
        ];

        yield return
        [
            "ExecuteResult",
            """{ "status": "Succeeded", "message": "pong", "errorCode": null, "output": { "message": "pong" } }"""
        ];

        yield return
        [
            "ValidateResponse",
            """["The 'service' parameter is required."]"""
        ];
    }

    [Theory]
    [MemberData(nameof(FixtureResponses))]
    public void Fixture_response_matches_its_openapi_schema(string schemaName, string json)
    {
        var schemas = OpenApi.RootElement.GetProperty("components").GetProperty("schemas");
        var fixture = JsonDocument.Parse(json).RootElement;

        AssertMatches(schemas.GetProperty(schemaName), fixture, schemas);
    }

    private static void AssertMatches(JsonElement schema, JsonElement value, JsonElement schemas)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var name = reference.GetString()!.Split('/').Last();
            AssertMatches(schemas.GetProperty(name), value, schemas);
            return;
        }

        if (schema.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean() && value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (schema.TryGetProperty("enum", out var allowed))
        {
            var values = allowed.EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.Contains(value.GetString(), values);
        }

        var type = schema.GetProperty("type").GetString();
        switch (type)
        {
            case "object":
                Assert.Equal(JsonValueKind.Object, value.ValueKind);
                if (schema.TryGetProperty("required", out var required))
                {
                    foreach (var property in required.EnumerateArray())
                    {
                        var name = property.GetString()!;
                        Assert.True(value.TryGetProperty(name, out _), $"Required property '{name}' is missing.");
                    }
                }

                schema.TryGetProperty("properties", out var properties);
                schema.TryGetProperty("additionalProperties", out var additional);
                foreach (var property in value.EnumerateObject())
                {
                    if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Name, out var propertySchema))
                    {
                        AssertMatches(propertySchema, property.Value, schemas);
                    }
                    else if (additional.ValueKind == JsonValueKind.Object)
                    {
                        AssertMatches(additional, property.Value, schemas);
                    }
                }

                break;

            case "array":
                Assert.Equal(JsonValueKind.Array, value.ValueKind);
                if (schema.TryGetProperty("items", out var items))
                {
                    foreach (var item in value.EnumerateArray())
                    {
                        AssertMatches(items, item, schemas);
                    }
                }

                break;

            case "string":
                Assert.Equal(JsonValueKind.String, value.ValueKind);
                break;

            case "boolean":
                Assert.Contains(value.ValueKind, new[] { JsonValueKind.True, JsonValueKind.False });
                break;

            case "number":
            case "integer":
                Assert.Equal(JsonValueKind.Number, value.ValueKind);
                break;
        }
    }

    private static JsonDocument LoadOpenApi()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "docs", "external-plugins.openapi.json")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return JsonDocument.Parse(
            File.ReadAllText(Path.Combine(directory!.FullName, "docs", "external-plugins.openapi.json")));
    }
}
