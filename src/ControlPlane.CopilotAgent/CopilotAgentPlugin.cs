using ControlPlane.Core.Plugins;
using ControlPlane.CopilotAgent.Agent;
using ControlPlane.CopilotAgent.Operations;
using ControlPlane.CopilotAgent.StatusProviders;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.CopilotAgent;

/// <summary>
/// Registers Copilot Agent status providers and operations with the Control Plane.
///
/// The plugin surfaces GitHub Copilot CLI (<c>gh copilot</c>) as two explicit, named
/// operations — <em>suggest</em> and <em>explain</em> — plus a liveness status signal
/// indicating whether the CLI is reachable and authenticated.
///
/// All operations are advisory: suggested commands are returned for human review and
/// are never executed automatically by the Control Plane.
///
/// Configuration keys (see <see cref="CopilotAgentSettings"/> and <c>.env</c>):
/// <list type="bullet">
///   <item><c>CopilotAgent__Enabled</c> — set to <c>true</c> to activate agent operations.</item>
///   <item><c>CopilotAgent__CliPath</c> — path to the CLI executable (default: <c>gh copilot</c>).</item>
///   <item><c>CopilotAgent__WorkingDirectory</c> — working directory for CLI invocations.</item>
///   <item><c>CopilotAgent__TimeoutSeconds</c> — per-invocation timeout (default: 120).</item>
/// </list>
/// </summary>
public sealed class CopilotAgentPlugin : IControlPlanePlugin, IPluginServiceRegistration
{
    public string Name => "CopilotAgent";

    /// <summary>
    /// Registers the <see cref="ICopilotAgentRunner"/> implementation into the DI container.
    /// Called before <see cref="Register"/> so the runner is available when operations are resolved.
    /// </summary>
    public void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<ICopilotAgentRunner, ProcessCopilotAgentRunner>();
    }

    public void Register(IPluginRegistration registration)
    {
        registration.AddStatusProvider<CopilotAgentAvailabilityStatusProvider>();
        registration.AddOperation<TriggerCopilotSuggestOperation>();
        registration.AddOperation<TriggerCopilotExplainOperation>();
    }
}
