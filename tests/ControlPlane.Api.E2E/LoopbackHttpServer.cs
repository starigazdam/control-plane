using System.Net;
using System.Net.Sockets;
namespace ControlPlane.Api.E2E;
/// <summary>Minimal loopback HTTP server so E2E tests never contact a live endpoint.</summary>
internal sealed class LoopbackHttpServer : IDisposable
{
    private const int MaxStartAttempts = 10;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancellation = new();
    public LoopbackHttpServer()
    {
        var (listener, url) = StartListener();
        _listener = listener;
        Url = url;
        _ = Task.Run(PumpAsync);
    }
    public string Url { get; }
    private async Task PumpAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_cancellation.Token); }
            catch (Exception) { return; }
            _ = Task.Run(async () =>
            {
                using (client)
                {
                    try
                    {
                        await client.GetStream().ReadAtLeastAsync(new byte[1024], 1, throwOnEndOfStream: false, _cancellation.Token);
                        await client.GetStream().WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(), _cancellation.Token);
                    }
                    catch (Exception) { }
                }
            });
        }
    }
    private static (TcpListener Listener, string Url) StartListener()
    {
        SocketException? lastException = null;
        for (var attempt = 0; attempt < MaxStartAttempts; attempt++)
        {
            var port = GetFreePort();
            var listener = new TcpListener(IPAddress.Loopback, port);
            try
            {
                listener.Start();
                return (listener, $"http://localhost:{port}/health?probe=r2");
            }
            catch (SocketException exception)
            {
                lastException = exception;
                try { listener.Stop(); } catch (ObjectDisposedException) { }
            }
        }
        throw (Exception?)lastException ?? new InvalidOperationException("Failed to start the loopback HTTP server.");
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
        _cancellation.Dispose();
    }
}
