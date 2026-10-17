using System.Net;
using ControlPlane.Api.Tests.Support;
using ControlPlane.Core.Operations;
using ControlPlane.HttpEndpoint;
using ControlPlane.HttpEndpoint.Operations;
namespace ControlPlane.Api.Tests;
public sealed class HttpEndpointOperationTests
{
    private const string Url = "http://localhost/private?token=secret-value";
    [Fact]
    public async Task Validation_rejects_missing_endpoint_id()
    {
        var errors = await CreateOperation(Configuration("alpha"), 200).ValidateAsync(Context(null), CancellationToken.None);
        Assert.Contains($"The '{CheckHttpEndpointOperation.ParameterName}' parameter is required.", errors);
    }
    [Fact]
    public async Task Validation_rejects_unknown_id_without_echoing_the_input()
    {
        var error = Assert.Single(await CreateOperation(Configuration("alpha"), 200).ValidateAsync(Context("not-configured"), CancellationToken.None));
        Assert.DoesNotContain("not-configured", error);
    }
    [Fact]
    public async Task Execute_returns_a_safe_summary_for_the_expected_status()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var result = await CreateOperation(Configuration("alpha"), handler).ExecuteAsync(Context("alpha"), CancellationToken.None);
        Assert.Equal(OperationExecutionStatus.Succeeded, result.Status);
        Assert.Null(result.ErrorCode);
        Assert.Equal("alpha", result.Output["endpointId"]);
        Assert.Equal("200", result.Output["observedStatus"]);
        Assert.True(result.Output.ContainsKey("observedAtUtc"));
        Assert.True(result.Output.ContainsKey("durationMs"));
        Assert.Equal(1, handler.RequestCount);
    }
    [Fact]
    public async Task Execute_runs_a_fresh_check_each_time_without_a_cache()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var operation = CreateOperation(Configuration("alpha"), handler);
        await operation.ExecuteAsync(Context("alpha"), CancellationToken.None);
        await operation.ExecuteAsync(Context("alpha"), CancellationToken.None);
        Assert.Equal(2, handler.RequestCount);
    }
    [Theory]
    [InlineData("unexpected", EndpointCheckOutcome.UnexpectedStatus)]
    [InlineData("timeout", EndpointCheckOutcome.Timeout)]
    [InlineData("transport", EndpointCheckOutcome.RequestFailed)]
    public async Task Execute_maps_failures_to_stable_error_codes_without_retry(string kind, string expectedErrorCode)
    {
        var handler = kind switch
        {
            "unexpected" => new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))),
            "timeout" => new StubHttpMessageHandler(async (_, token) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
            _ => new StubHttpMessageHandler((_, _) => throw new HttpRequestException("down"))
        };
        var timeout = kind == "timeout" ? TimeSpan.FromMilliseconds(50) : (TimeSpan?)null;
        var result = await CreateOperation(Configuration("alpha", timeout: timeout), handler).ExecuteAsync(Context("alpha"), CancellationToken.None);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Equal(OperationExecutionStatus.Failed, result.Status);
        Assert.Equal(1, handler.RequestCount);
    }
    [Fact]
    public async Task Execute_never_echoes_url_display_name_or_exception_text()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("boom-secret-exception"));
        var result = await CreateOperation(Configuration("alpha", name: "Very Secret Display Name"), handler).ExecuteAsync(Context("alpha"), CancellationToken.None);
        Assert.DoesNotContain(Url, result.Message);
        Assert.DoesNotContain("Very Secret Display Name", result.Message);
        Assert.DoesNotContain("boom-secret-exception", result.Message);
        foreach (var value in result.Output.Values)
        {
            Assert.DoesNotContain(Url, value);
            Assert.DoesNotContain("Very Secret Display Name", value);
        }
    }
    private static CheckHttpEndpointOperation CreateOperation(HttpEndpointConfiguration configuration, int statusCode)
        => CreateOperation(configuration, new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)statusCode))));
    private static CheckHttpEndpointOperation CreateOperation(HttpEndpointConfiguration configuration, StubHttpMessageHandler handler)
        => new(configuration, new HttpEndpointChecker(new StubHttpClientFactory(handler)));
    private static OperationExecutionContext Context(string? endpointId)
    {
        var input = new Dictionary<string, string?>();
        if (endpointId is not null) input[CheckHttpEndpointOperation.ParameterName] = endpointId;
        return new OperationExecutionContext(
            Request: new OperationRequest("placeholder", CheckHttpEndpointOperation.OperationId, input),
            InitiatedBy: "test",
            RequestedAtUtc: DateTimeOffset.UtcNow,
            CorrelationId: null);
    }
    private static HttpEndpointConfiguration Configuration(string id, string name = "Endpoint Alpha", TimeSpan? timeout = null)
        => new(
            isEnabled: true,
            endpoints: [new HttpEndpointDefinition(id, name, Url, 200, "GET", timeout ?? TimeSpan.FromSeconds(5))],
            warnings: [],
            hasScheduleWarning: false);
}
