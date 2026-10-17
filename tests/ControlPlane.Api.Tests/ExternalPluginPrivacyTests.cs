using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using ControlPlane.Api.Tests.Support;
using ControlPlane.ExternalPlugins;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.Api.Tests;

public sealed class ExternalPluginPrivacyTests
{
    [Fact]
    public void Socket_handler_disables_redirects_and_trace_propagation()
    {
        using var handler = ExternalPluginsHttpClient.CreateSocketHandler();

        Assert.False(handler.AllowAutoRedirect);

        using var activity = new Activity("test").Start();
        var carrier = new Dictionary<string, string?>();
        handler.ActivityHeadersPropagator!.Inject(
            activity,
            carrier,
            static (headers, key, value) => ((Dictionary<string, string?>)headers!)[key] = value);

        Assert.Empty(carrier);
    }

    [Fact]
    public async Task Named_client_sends_no_trace_context_or_identity_headers()
    {
        var received = new TaskCompletionSource<RequestCapture>();
        using var server = new LoopbackHttpServer(async (context, token) =>
        {
            string body;
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                body = await reader.ReadToEndAsync(token);
            }

            received.TrySetResult(new RequestCapture(
                context.Request.Headers["traceparent"],
                context.Request.Headers["tracestate"],
                context.Request.Headers["authorization"],
                body));

            context.Response.StatusCode = 200;
            context.Response.Close();
        });

        var services = new ServiceCollection();
        services.AddLogging();
        ExternalPluginsHttpClient.Register(services);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(ExternalPluginsHttpClient.Name);

        using var activity = new Activity("test").Start();
        using var content = new StringContent("{\"projectId\":\"p\"}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(server.Url("operations/ping/execute"), content);
        var capture = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Null(capture.TraceParent);
        Assert.Null(capture.TraceState);
        Assert.Null(capture.Authorization);
        Assert.Equal("{\"projectId\":\"p\"}", capture.Body);
    }

    private sealed record RequestCapture(
        string? TraceParent,
        string? TraceState,
        string? Authorization,
        string Body);
}
