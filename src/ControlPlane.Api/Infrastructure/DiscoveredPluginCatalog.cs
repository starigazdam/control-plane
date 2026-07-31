namespace ControlPlane.Api.Infrastructure;

public sealed record DiscoveredPluginCatalog(
    IReadOnlyList<string> PluginNames,
    IReadOnlyList<Type> StatusProviderTypes,
    IReadOnlyList<Type> OperationTypes);
