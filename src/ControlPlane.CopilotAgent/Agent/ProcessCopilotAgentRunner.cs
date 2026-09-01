using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlPlane.CopilotAgent.Agent;

/// <summary>
/// Shells out to <c>gh copilot</c> (or a configured path) to fulfil agent requests.
/// Each invocation is a separate process and is bounded by <see cref="CopilotAgentSettings.TimeoutSeconds"/>.
/// </summary>
public sealed class ProcessCopilotAgentRunner : ICopilotAgentRunner
{
    private readonly CopilotAgentSettings _settings;
    private readonly ILogger<ProcessCopilotAgentRunner> _logger;

    public ProcessCopilotAgentRunner(
        IOptions<CopilotAgentSettings> settings,
        ILogger<ProcessCopilotAgentRunner> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task<AgentInvocationResult> SuggestAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        // gh copilot suggest accepts the prompt as a positional argument.
        // --target shell is the default mode that returns a runnable command.
        return RunAsync(["suggest", "--target", "shell", prompt], cancellationToken);
    }

    public Task<AgentInvocationResult> ExplainAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        return RunAsync(["explain", subject], cancellationToken);
    }

    private async Task<AgentInvocationResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        // Split "gh copilot" → executable="gh", base-args=["copilot"]
        var parts = _settings.CliPath.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var executable = parts[0];
        var baseArgs = parts.Length > 1 ? parts[1..] : [];
        var allArgs = baseArgs.Concat(arguments).ToArray();

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(_settings.WorkingDirectory))
        {
            startInfo.WorkingDirectory = _settings.WorkingDirectory;
        }

        foreach (var arg in allArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var stopwatch = Stopwatch.StartNew();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

        Process? process = null;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start process '{executable}'.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);
            stopwatch.Stop();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            _logger.LogInformation(
                "Copilot CLI exited with code {ExitCode} after {Duration}ms. Args: [{Args}]",
                process.ExitCode,
                stopwatch.ElapsedMilliseconds,
                string.Join(", ", allArgs));

            return new AgentInvocationResult(
                Succeeded: process.ExitCode == 0,
                Output: stdout.Trim(),
                ErrorOutput: string.IsNullOrWhiteSpace(stderr) ? null : stderr.Trim(),
                ExitCode: process.ExitCode,
                Duration: stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timed out — kill the process if still running
            try { process?.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            stopwatch.Stop();

            _logger.LogWarning(
                "Copilot CLI timed out after {TimeoutSeconds}s. Args: [{Args}]",
                _settings.TimeoutSeconds,
                string.Join(", ", allArgs));

            return new AgentInvocationResult(
                Succeeded: false,
                Output: string.Empty,
                ErrorOutput: $"Copilot CLI timed out after {_settings.TimeoutSeconds}s.",
                ExitCode: -1,
                Duration: stopwatch.Elapsed);
        }
        finally
        {
            process?.Dispose();
        }
    }
}
