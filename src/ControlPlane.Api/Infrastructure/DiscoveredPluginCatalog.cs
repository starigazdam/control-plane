namespace ControlPlane.Api.Infrastructure;

/// <summary>
/// A recorded status-provider or operation registration. Exactly one of
/// <see cref="ImplementationType"/>, <see cref="Instance"/>, or <see cref="Factory"/>
/// is set, matching how the plugin registered the capability.
/// </summary>
public sealed record PluginCapabilityDescriptor(
    Type ContractType,
    Type? ImplementationType,
    object? Instance,
    Func<IServiceProvider, object>? Factory);

public sealed record DiscoveredPluginCatalog(
    IReadOnlyList<string> PluginNames,
    IReadOnlyList<PluginCapabilityDescriptor> StatusProviders,
    IReadOnlyList<PluginCapabilityDescriptor> Operations);
