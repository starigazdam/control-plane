using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ControlPlane.Api.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace ControlPlane.Api.Tests.Support;
internal sealed class LoopbackHttpServer : IDisposable
{
    private const int MaxStartAttempts = 10;
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Func<HttpListenerContext, CancellationToken, Task> _handler;
    private readonly Task _pump;
    private int _requestCount;
    public LoopbackHttpServer(Func<HttpListenerContext, CancellationToken, Task> handler)
    {
        _handler = handler;
        var (listener, baseAddress) = StartListener();
        _listener = listener;
        BaseAddress = baseAddress;
        _pump = Task.Run(PumpAsync);
    }
    public Uri BaseAddress { get; }
    public int RequestCount => Volatile.Read(ref _requestCount);
    public string Url(string pathAndQuery = "health") => new Uri(BaseAddress, pathAndQuery).ToString();
    public static Task WriteStatusAsync(HttpListenerContext context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentLength64 = 0;
        context.Response.Close();
        return Task.CompletedTask;
    }
    private async Task PumpAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync().ConfigureAwait(false); }
            catch (Exception) { return; }
            Interlocked.Increment(ref _requestCount);
            _ = Task.Run(
                async () =>
                {
                    try { await _handler(context, _cancellation.Token).ConfigureAwait(false); } catch (Exception) { }
                },
                _cancellation.Token);
        }
    }
    private static (HttpListener Listener, Uri BaseAddress) StartListener()
    {
        HttpListenerException? lastException = null;
        for (var attempt = 0; attempt < MaxStartAttempts; attempt++)
        {
            var port = GetFreePort();
            var baseAddress = new Uri($"http://localhost:{port}/");
            var listener = new HttpListener();
            listener.Prefixes.Add(baseAddress.ToString());
            try
            {
                listener.Start();
                return (listener, baseAddress);
            }
            catch (HttpListenerException exception)
            {
                lastException = exception;
                try { listener.Close(); } catch (ObjectDisposedException) { }
            }
        }
        throw (Exception?)lastException ?? new InvalidOperationException("Failed to start the loopback HTTP listener.");
    }
    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
    public void Dispose()
    {
        _cancellation.Cancel();
        try { _listener.Stop(); } catch (ObjectDisposedException) { }
        _listener.Close();
        try { _pump.Wait(TimeSpan.FromSeconds(5)); } catch (AggregateException) { }
        _cancellation.Dispose();
    }
}
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
    private int _requestCount;
    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        => _responder = responder;
    public int RequestCount => Volatile.Read(ref _requestCount);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);
        return await _responder(request, cancellationToken).ConfigureAwait(false);
    }
}
internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly StubHttpMessageHandler _handler;
    public StubHttpClientFactory(StubHttpMessageHandler handler) => _handler = handler;
    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}
internal sealed class RealHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new() { Timeout = Timeout.InfiniteTimeSpan };
}
internal sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
{
    private readonly CancellationTokenSource _stopping = new();
    public CancellationToken ApplicationStarted => CancellationToken.None;
    public CancellationToken ApplicationStopping => _stopping.Token;
    public CancellationToken ApplicationStopped => CancellationToken.None;
    public void StopApplication() => _stopping.Cancel();
}
internal static class TestServiceProvider
{
    public static ServiceProvider Create(Action<ILoggingBuilder>? configureLogging = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            configureLogging?.Invoke(builder);
        });
        services.AddServiceDiscovery();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });
        services.AddControlPlanePlugins(
            new ConfigurationBuilder().Build(),
            typeof(ControlPlane.HttpEndpoint.HttpEndpointPlugin).Assembly);
        return services.BuildServiceProvider();
    }
}
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Messages);
    public void Dispose() { }
    private sealed class CapturingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _messages;
        public CapturingLogger(string categoryName, ConcurrentQueue<string> messages)
        {
            CategoryName = categoryName;
            _messages = messages;
        }
        public string CategoryName { get; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (exception is not null) message = $"{message} {exception}";
            _messages.Enqueue($"[{CategoryName}] {message}");
        }
    }
}
