using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ControlPlane.Api.E2E;

/// <summary>
/// Checks that the live fixture responses match the published OpenAPI contract
/// without a validator package: required properties are present and carry the
/// declared JSON types. The fixture runs as a real process, so fixture drift from
/// the published contract fails this test.
/// </summary>
public sealed class ExternalPluginsOpenApiTests
{
    private static readonly JsonDocument OpenApi = LoadOpenApi();

    [Fact]
    public async Task Live_fixture_responses_match_their_openapi_schemas()
    {
        await using var fixture = await FixtureProcess.StartAsync("normal");
        using var client = new HttpClient { BaseAddress = new Uri(fixture.BaseUrl) };

        var schemas = OpenApi.RootElement.GetProperty("components").GetProperty("schemas");

        AssertMatches(
            schemas.GetProperty("Capabilities"),
            await GetJsonAsync(client, "/capabilities"),
            schemas);

        AssertMatches(
            schemas.GetProperty("StatusSnapshot"),
            await GetJsonAsync(client, "/status/health?projectId=placeholder"),
            schemas);

        var request = new { projectId = "placeholder", input = new Dictionary<string, string?>() };

        var validateResponse = await client.PostAsJsonAsync("/operations/ping/validate", request);
        Assert.Equal(HttpStatusCode.OK, validateResponse.StatusCode);
        AssertMatches(
            schemas.GetProperty("ValidateResponse"),
            await ParseAsync(validateResponse),
            schemas);

        var executeResponse = await client.PostAsJsonAsync("/operations/ping/execute", request);
        Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);
        AssertMatches(
            schemas.GetProperty("ExecuteResult"),
            await ParseAsync(executeResponse),
            schemas);
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ParseAsync(response);
    }

    private static async Task<JsonElement> ParseAsync(HttpResponseMessage response)
    {
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(document);
        return document!.RootElement.Clone();
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
