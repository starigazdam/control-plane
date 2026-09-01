namespace ControlPlane.CopilotAgent;

/// <summary>
/// Configuration for the Copilot Agent plugin.
/// Loaded from the <c>CopilotAgent</c> section of the environment / .env file.
/// </summary>
public sealed class CopilotAgentSettings
{
    public const string SectionName = "CopilotAgent";

    /// <summary>
    /// Path to the <c>gh</c> executable.
    /// Defaults to <c>gh</c>, which requires it to be on <c>PATH</c>.
    /// Use an absolute path when the executable is not on <c>PATH</c> at API startup
    /// (e.g. <c>/usr/local/bin/gh</c>).
    /// </summary>
    public string CliExecutable { get; set; } = "gh";

    /// <summary>
    /// Arguments passed to <see cref="CliExecutable"/> before any operation-specific arguments.
    /// Defaults to <c>copilot</c>, which selects the Copilot CLI extension.
    /// Change this if the extension is invoked differently in your environment.
    /// </summary>
    public string CliBaseArgs { get; set; } = "copilot";

    /// <summary>
    /// Working directory for Copilot CLI invocations.
    /// When empty, the current working directory of the API process is used.
    /// Must be an existing directory when non-empty; validated at startup.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Maximum time (in seconds) to wait for a single Copilot CLI call to complete.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// When <c>true</c>, the agent runner is enabled and status/operations are functional.
    /// Set to <c>false</c> to disable all Copilot Agent operations without removing the plugin.
    /// </summary>
    public bool Enabled { get; set; } = false;
}
