using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace ControlPlane.Api.E2E;

/// <summary>
/// Starts the fabricated external plugin as a separate Kestrel process on a free
/// loopback port and waits until it answers. The plugin process never talks to a
/// live external system. Always dispose this type to kill the process.
/// </summary>
internal sealed class FixtureProcess : IAsyncDisposable, IDisposable
{
    private readonly Process _process;
    private readonly HttpClient _probe = new() { Timeout = TimeSpan.FromSeconds(2) };
    private bool _disposed;

    private FixtureProcess(Process process, int port)
    {
        _process = process;
        Port = port;
    }

    public int Port { get; }

    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public static async Task<FixtureProcess> StartAsync(string mode)
    {
        var port = GetFreePort();
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "ControlPlane.ExternalPlugins.Fixture.dll");

        if (!File.Exists(fixturePath))
        {
            throw new InvalidOperationException($"Fixture assembly not found at '{fixturePath}'.");
        }

        var startInfo = new ProcessStartInfo(RuntimePath())
        {
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(fixturePath);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add($"http://127.0.0.1:{port}");
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add(mode);

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the fixture process.");

        var fixture = new FixtureProcess(process, port);
        try
        {
            await fixture.WaitForReadyAsync().ConfigureAwait(false);
            return fixture;
        }
        catch
        {
            fixture.Dispose();
            throw;
        }
    }

    private async Task WaitForReadyAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The fixture process exited early with code {_process.ExitCode}.");
            }

            try
            {
                using var response = await _probe.GetAsync($"{BaseUrl}/capabilities").ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        throw new TimeoutException("The fixture process did not become ready in time.");
    }

    private static string RuntimePath()
    {
        var processPath = Environment.ProcessPath;
        return processPath is not null && processPath.EndsWith("dotnet", StringComparison.OrdinalIgnoreCase)
            ? processPath
            : "dotnet";
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _probe.Dispose();

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception)
        {
        }

        _process.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
