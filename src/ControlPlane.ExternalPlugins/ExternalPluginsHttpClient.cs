using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.ExternalPlugins;

/// <summary>
/// Owns the single named client shared by every external bridge and the short-lived
/// startup client. Both disable redirects, cookies, and trace-context propagation;
/// the named runtime client additionally removes the ServiceDefaults resilience and
/// service-discovery handlers and the framework request loggers, and leaves the
/// per-call timeout to the bridge's linked cancellation token source.
/// </summary>
internal static class ExternalPluginsHttpClient
{
    public const string Name = "ControlPlane.ExternalPlugins";

    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public static void Register(IServiceCollection services)
    {
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is experimental but is the supported opt-out.
        var builder = services
            .AddHttpClient(Name)
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        builder
            .RemoveAllLoggers()
            .ConfigureHttpClient(static client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigureAdditionalHttpMessageHandlers(static (handlers, _) =>
            {
                for (var index = handlers.Count - 1; index >= 0; index--)
                {
                    if (string.Equals(
                            handlers[index].GetType().FullName,
                            "Microsoft.Extensions.ServiceDiscovery.Http.ResolvingHttpDelegatingHandler",
                            StringComparison.Ordinal))
                    {
                        handlers.RemoveAt(index);
                    }
                }
            })
            .ConfigurePrimaryHttpMessageHandler(static () => CreateSocketHandler());
    }

    public static SocketsHttpHandler CreateSocketHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        ActivityHeadersPropagator = DistributedContextPropagator.CreateNoOutputPropagator()
    };

    public static HttpClient CreateStartupClient() => new(CreateSocketHandler(), disposeHandler: true)
    {
        Timeout = Timeout.InfiniteTimeSpan
    };
}
