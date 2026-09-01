namespace ControlPlane.CopilotAgent;

/// <summary>
/// Configuration for the Copilot Agent plugin.
/// Loaded from the <c>CopilotAgent</c> section of the environment / .env file.
/// </summary>
public sealed class CopilotAgentSettings
{
    public const string SectionName = "CopilotAgent";

    /// <summary>
    /// Path to the GitHub Copilot CLI executable (e.g. <c>gh copilot</c> or an absolute path).
    /// Defaults to <c>gh copilot</c>, which requires the Copilot CLI extension to be installed.
    /// </summary>
    public string CliPath { get; set; } = "gh copilot";

    /// <summary>
    /// Working directory for Copilot CLI invocations.
    /// When empty, the current working directory of the API process is used.
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
