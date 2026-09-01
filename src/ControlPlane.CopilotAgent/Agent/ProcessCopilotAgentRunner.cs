using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlPlane.CopilotAgent.Agent;

/// <summary>
/// Shells out to <c>gh copilot</c> (or a configured path) to fulfil agent requests.
/// Each invocation is a separate process and is bounded by <see cref="CopilotAgentSettings.TimeoutSeconds"/>.
///
/// <para><strong>CliPath note:</strong> the configured <see cref="CopilotAgentSettings.CliPath"/>
/// is split on the first space only to separate the executable from the base arguments
/// (e.g. <c>"gh copilot"</c> → executable=<c>"gh"</c>, base-arg=<c>"copilot"</c>).
/// Paths containing spaces must be quoted or the executable separated from its
/// arguments using the <c>CopilotAgent__CliPath</c> convention.</para>
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

    /// <inheritdoc/>
    public Task<AgentInvocationResult> ProbeAsync(CancellationToken cancellationToken)
    {
        // "gh --version" (no copilot subcommand) returns exit 0 and prints the gh version
        // when the CLI is installed and on PATH. We cannot use "gh copilot --version"
        // because the Copilot extension treats positional arguments as the text to explain.
        var parts = _settings.CliPath.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var executable = parts[0];
        return RunAsync(executable, ["--version"], cancellationToken);
    }

    /// <inheritdoc/>
    public Task<AgentInvocationResult> SuggestAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        // gh copilot suggest accepts the prompt as a positional argument.
        // --target shell is the default mode that returns a runnable command.
        return RunCliAsync(["suggest", "--target", "shell", prompt], cancellationToken);
    }

    /// <inheritdoc/>
    public Task<AgentInvocationResult> ExplainAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        return RunCliAsync(["explain", subject], cancellationToken);
    }

    /// <summary>Runs the configured CLI with <paramref name="subcommandArgs"/> appended.</summary>
    private Task<AgentInvocationResult> RunCliAsync(
        IReadOnlyList<string> subcommandArgs,
        CancellationToken cancellationToken)
    {
        // Split "gh copilot" → executable="gh", base-args=["copilot"]
        var parts = _settings.CliPath.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var executable = parts[0];
        var allArgs = parts.Length > 1
            ? parts[1..].Concat(subcommandArgs).ToArray()
            : subcommandArgs.ToArray();

        return RunAsync(executable, allArgs, cancellationToken);
    }

    private async Task<AgentInvocationResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
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

        foreach (var arg in arguments)
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
                string.Join(", ", arguments));

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
                string.Join(", ", arguments));

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
