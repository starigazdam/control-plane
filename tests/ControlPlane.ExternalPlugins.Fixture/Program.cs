// Minimal fabricated external plugin used only by the Control Plane E2E tests.
// It serves the external-plugin wire contract over loopback HTTP and never
// contacts anything external. Run with:
//   dotnet ControlPlane.ExternalPlugins.Fixture.dll --urls http://127.0.0.1:<port> --mode normal
//
// Modes:
//   normal               - a clean capabilities response and successful calls
//   slow-execute         - POST /operations/{id}/execute stalls past the bridge timeout
//   invalid-capabilities - a capabilities body missing the required arrays
//   excluded-id          - a capabilities body with an invalid and a reserved capability
//   invalid-validate     - POST /operations/{id}/validate returns a non-2xx status

using System.Text.Json;

namespace ControlPlane.ExternalPlugins.Fixture;

// A named entry point avoids a second global `Program` type in the test
// assembly, which would otherwise collide with ControlPlane.Api's Program.
public static class FixtureApp
{
    public static void Main(string[] args)
    {
        var mode = ReadArgument(args, "--mode") ?? "normal";

        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        var app = builder.Build();

        app.MapGet("/capabilities", () =>
        {
            if (mode == "invalid-capabilities")
            {
                return Results.Json(new { statusProviderIds = new[] { "health" } });
            }

            var operations = new List<object>
            {
                new
                {
                    id = "ping",
                    displayName = "Ping",
                    description = "Returns a pong result.",
                    requiresConfirmation = false,
                    parameters = Array.Empty<object>()
                },
                new
                {
                    id = "restart",
                    displayName = "Restart",
                    description = "Restarts a named service.",
                    requiresConfirmation = true,
                    parameters = new object[]
                    {
                        new
                        {
                            name = "service",
                            displayName = "Service",
                            description = "Service to restart.",
                            isRequired = true,
                            type = "string",
                            defaultValue = (string?)null
                        }
                    }
                }
            };

            var statusProviderIds = new List<object> { "health" };

            if (mode == "excluded-id")
            {
                statusProviderIds.Add("bridge");
                statusProviderIds.Add(42);
                operations.Add(new
                {
                    id = "bad id!",
                    displayName = "Bad id",
                    description = "An operation whose remote id is not valid.",
                    requiresConfirmation = false,
                    parameters = Array.Empty<object>()
                });
            }

            return Results.Json(new { statusProviderIds, operations });
        });

        app.MapGet("/status/{id}", (string id, string? projectId) => Results.Json(new
        {
            projectId = projectId ?? string.Empty,
            providerId = id,
            overallLevel = "Healthy",
            observedAtUtc = DateTimeOffset.UtcNow,
            signals = new object[]
            {
                new
                {
                    id = "ok",
                    title = "Healthy",
                    description = "The fabricated plugin is healthy.",
                    level = "Healthy",
                    observedAtUtc = DateTimeOffset.UtcNow,
                    source = (string?)null,
                    link = (string?)null
                }
            }
        }));

        app.MapPost("/operations/{id}/validate", async (HttpRequest request, string id) =>
        {
            if (mode == "invalid-validate")
            {
                return Results.StatusCode(500);
            }

            var body = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
            var errors = new List<string>();

            if (id == "restart" && !HasInput(body, "service"))
            {
                errors.Add("The 'service' parameter is required.");
            }

            return Results.Json(errors);
        });

        app.MapPost("/operations/{id}/execute", async (HttpRequest request, string id) =>
        {
            if (mode == "slow-execute")
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
            }

            await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);

            return Results.Json(new
            {
                status = "Succeeded",
                message = id == "ping" ? "pong" : "done",
                errorCode = (string?)null,
                output = new Dictionary<string, string?> { ["message"] = id == "ping" ? "pong" : "done" }
            });
        });

        app.Run();
    }

    private static bool HasInput(JsonElement body, string name)
    {
        return body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty("input", out var input)
            && input.ValueKind == JsonValueKind.Object
            && input.TryGetProperty(name, out var value)
            && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
    }

    private static string? ReadArgument(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
