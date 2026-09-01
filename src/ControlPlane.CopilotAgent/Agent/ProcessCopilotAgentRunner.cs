using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlPlane.CopilotAgent.Agent;

/// <summary>
/// Shells out to the configured <c>gh</c> executable to fulfil Copilot Agent requests.
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

        ValidateSettings(_settings);
    }

    /// <inheritdoc/>
    public Task<AgentInvocationResult> ProbeAsync(CancellationToken cancellationToken)
    {
        // Run "gh --version" — a fast, non-interactive check that the gh executable
        // is present and on PATH. Does NOT include the Copilot base args because we
        // want to probe the executable itself, not the extension.
        return RunAsync(_settings.CliExecutable, ["--version"], cancellationToken);
    }

    /// <inheritdoc/>
    public Task<AgentInvocationResult> SuggestAsync(string prompt, CancellationToken cancellationToken)
    {
        // gh copilot suggest accepts the prompt as a positional argument.
        // --target shell is the default mode that returns a runnable command.
        return RunCliAsync(["suggest", "--target", "shell", prompt], cancellationToken);
    }

    /// <inheritdoc/>
    public Task<AgentInvocationResult> ExplainAsync(string subject, CancellationToken cancellationToken)
    {
        return RunCliAsync(["explain", subject], cancellationToken);
    }

    /// <summary>Prepends <see cref="CopilotAgentSettings.CliBaseArgs"/> and runs the CLI.</summary>
    private Task<AgentInvocationResult> RunCliAsync(
        IReadOnlyList<string> subcommandArgs,
        CancellationToken cancellationToken)
    {
        var baseArgs = _settings.CliBaseArgs
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var allArgs = baseArgs.Concat(subcommandArgs).ToArray();
        return RunAsync(_settings.CliExecutable, allArgs, cancellationToken);
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

    private static void ValidateSettings(CopilotAgentSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.CliExecutable))
        {
            throw new InvalidOperationException(
                "CopilotAgent__CliExecutable must not be empty. " +
                "Set it to 'gh' or an absolute path to the gh executable.");
        }

        if (!string.IsNullOrWhiteSpace(settings.WorkingDirectory) &&
            !Directory.Exists(settings.WorkingDirectory))
        {
            throw new InvalidOperationException(
                $"CopilotAgent__WorkingDirectory '{settings.WorkingDirectory}' does not exist. " +
                "Create the directory or clear the setting to use the API process's working directory.");
        }
    }
}
