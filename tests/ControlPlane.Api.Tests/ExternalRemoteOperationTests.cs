using System.Net;
using System.Text;
using ControlPlane.Api.Tests.Support;
using ControlPlane.Core.Operations;
using ControlPlane.ExternalPlugins;

namespace ControlPlane.Api.Tests;

public sealed class ExternalRemoteOperationTests
{
    [Fact]
    public async Task Execute_passes_a_well_formed_result_through_and_uses_its_own_timestamps()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json("""
            {
              "status": "Succeeded",
              "message": "ok",
              "startedAtUtc": "2000-01-01T00:00:00+00:00",
              "completedAtUtc": "2000-01-01T00:00:00+00:00",
              "errorCode": "sample_ok",
              "output": { "x": "y", "z": null }
            }
            """)));

        var result = await CreateOperation(handler).ExecuteAsync(Context(), CancellationToken.None);

        Assert.Equal(OperationExecutionStatus.Succeeded, result.Status);
        Assert.Equal("ok", result.Message);
        Assert.Equal("sample_ok", result.ErrorCode);
        Assert.Equal("y", result.Output["x"]);
        Assert.Null(result.Output["z"]);
        Assert.True(result.StartedAtUtc.Year > 2020);
        Assert.True(result.CompletedAtUtc >= result.StartedAtUtc);
    }

    [Fact]
    public async Task Execute_uses_the_remote_id_and_execute_path()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            captured = request;
            return Task.FromResult(Json("""{ "status": "Succeeded", "message": "ok" }"""));
        });

        await CreateOperation(handler).ExecuteAsync(Context("proj"), CancellationToken.None);

        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("http://localhost:5099/operations/ping/execute", captured.RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("timeout", ExternalOperationErrorCodes.Timeout)]
    [InlineData("transport", ExternalOperationErrorCodes.TransportFailed)]
    [InlineData("status", ExternalOperationErrorCodes.UnexpectedStatus)]
    [InlineData("invalid", ExternalOperationErrorCodes.InvalidResponse)]
    [InlineData("unknown-status", ExternalOperationErrorCodes.InvalidResponse)]
    [InlineData("reserved-error-code", ExternalOperationErrorCodes.InvalidResponse)]
    [InlineData("bad-output", ExternalOperationErrorCodes.InvalidResponse)]
    [InlineData("missing-message", ExternalOperationErrorCodes.InvalidResponse)]
    public async Task Execute_maps_each_failure_to_a_stable_code(string kind, string expectedCode)
    {
        var handler = kind switch
        {
            "timeout" => new StubHttpMessageHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return Json("{}");
            }),
            "transport" => new StubHttpMessageHandler((_, _) => throw new HttpRequestException("down")),
            "status" => new StubHttpMessageHandler((_, _) => Task.FromResult(Json("{}", HttpStatusCode.InternalServerError))),
            "invalid" => new StubHttpMessageHandler((_, _) => Task.FromResult(Json("not json"))),
            "unknown-status" => new StubHttpMessageHandler((_, _) => Task.FromResult(Json("""{ "status": "Weird", "message": "m" }"""))),
            "reserved-error-code" => new StubHttpMessageHandler((_, _) => Task.FromResult(Json("""{ "status": "Failed", "message": "m", "errorCode": "external_operation_x" }"""))),
            "bad-output" => new StubHttpMessageHandler((_, _) => Task.FromResult(Json("""{ "status": "Succeeded", "message": "m", "output": { "x": 5 } }"""))),
            _ => new StubHttpMessageHandler((_, _) => Task.FromResult(Json("""{ "status": "Succeeded" }""")))
        };

        var timeout = kind == "timeout" ? TimeSpan.FromMilliseconds(50) : (TimeSpan?)null;
        var result = await CreateOperation(handler, timeout).ExecuteAsync(Context(), CancellationToken.None);

        Assert.Equal(OperationExecutionStatus.Failed, result.Status);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_instead_of_mapping_to_a_timeout()
    {
        var handler = new StubHttpMessageHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Json("{}");
        });
        using var cancellation = new CancellationTokenSource();
        var task = CreateOperation(handler, TimeSpan.FromSeconds(30)).ExecuteAsync(Context(), cancellation.Token);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Validate_returns_the_remote_error_strings()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(Json("""["a", "b"]""")));

        var errors = await CreateOperation(handler).ValidateAsync(Context(), CancellationToken.None);

        Assert.Equal(["a", "b"], errors);
    }

    [Fact]
    public async Task Validate_posts_the_project_id_and_input_to_the_validate_path()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var handler = new StubHttpMessageHandler(async (request, token) =>
        {
            captured = request;
            body = await request.Content!.ReadAsStringAsync(token);
            return Json("[]");
        });

        await CreateOperation(handler).ValidateAsync(Context("proj"), CancellationToken.None);

        Assert.Equal("http://localhost:5099/operations/ping/validate", captured!.RequestUri!.AbsoluteUri);
        Assert.Contains("\"projectId\":\"proj\"", body);
    }

    [Theory]
    [InlineData("timeout", ExternalOperationErrorCodes.Timeout)]
    [InlineData("transport", ExternalOperationErrorCodes.TransportFailed)]
    [InlineData("status", ExternalOperationErrorCodes.UnexpectedStatus)]
    [InlineData("malformed", ExternalOperationErrorCodes.InvalidResponse)]
    public async Task Validate_failure_returns_one_formatted_error_string(string kind, string expectedCode)
    {
        var handler = kind switch
        {
            "timeout" => new StubHttpMessageHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return Json("[]");
            }),
            "transport" => new StubHttpMessageHandler((_, _) => throw new HttpRequestException("down")),
            "status" => new StubHttpMessageHandler((_, _) => Task.FromResult(Json("[]", HttpStatusCode.BadGateway))),
            _ => new StubHttpMessageHandler((_, _) => Task.FromResult(Json("not an array")))
        };

        var timeout = kind == "timeout" ? TimeSpan.FromMilliseconds(50) : (TimeSpan?)null;
        var errors = await CreateOperation(handler, timeout).ValidateAsync(Context(), CancellationToken.None);

        var error = Assert.Single(errors);
        Assert.StartsWith($"{expectedCode}: ", error);
    }

    [Fact]
    public void Definition_is_namespaced_and_preserves_confirmation_metadata()
    {
        var operation = new ExternalRemoteOperation(
            "sample",
            new ExternalOperationDefinition("restart", "Restart", "Restarts.", true, []),
            new Uri("http://localhost:5099"),
            new StubHttpClientFactory(new StubHttpMessageHandler((_, _) => Task.FromResult(Json("[]")))),
            TimeSpan.FromSeconds(10));

        Assert.Equal("sample.restart", operation.Definition.Id);
        Assert.True(operation.Definition.RequiresConfirmation);
    }

    private static ExternalRemoteOperation CreateOperation(StubHttpMessageHandler handler, TimeSpan? timeout = null)
        => new(
            "sample",
            new ExternalOperationDefinition("ping", "Ping", "Pings.", false, []),
            new Uri("http://localhost:5099"),
            new StubHttpClientFactory(handler),
            timeout ?? TimeSpan.FromSeconds(10));

    private static OperationExecutionContext Context(string projectId = "placeholder")
        => new(
            new OperationRequest(projectId, "sample.ping", new Dictionary<string, string?>()),
            "test",
            DateTimeOffset.UtcNow,
            null);

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
}
