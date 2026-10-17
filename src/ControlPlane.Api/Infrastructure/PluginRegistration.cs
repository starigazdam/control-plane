using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Plugins;

namespace ControlPlane.Api.Infrastructure;

internal sealed class PluginRegistration : IPluginRegistration
{
    private readonly HashSet<Type> _statusProviderTypes = [];
    private readonly HashSet<Type> _operationTypes = [];
    private readonly List<PluginCapabilityDescriptor> _statusProviders = [];
    private readonly List<PluginCapabilityDescriptor> _operations = [];

    public IReadOnlyList<PluginCapabilityDescriptor> StatusProviders => _statusProviders;

    public IReadOnlyList<PluginCapabilityDescriptor> Operations => _operations;

    public void AddStatusProvider<TStatusProvider>() where TStatusProvider : class, IStatusProvider
    {
        if (_statusProviderTypes.Add(typeof(TStatusProvider)))
        {
            _statusProviders.Add(new PluginCapabilityDescriptor(
                typeof(IStatusProvider),
                typeof(TStatusProvider),
                Instance: null,
                Factory: null));
        }
    }

    public void AddOperation<TOperation>() where TOperation : class, IOperation
    {
        if (_operationTypes.Add(typeof(TOperation)))
        {
            _operations.Add(new PluginCapabilityDescriptor(
                typeof(IOperation),
                typeof(TOperation),
                Instance: null,
                Factory: null));
        }
    }

    public void AddStatusProvider(IStatusProvider statusProvider)
    {
        ArgumentNullException.ThrowIfNull(statusProvider);
        _statusProviders.Add(new PluginCapabilityDescriptor(
            typeof(IStatusProvider),
            ImplementationType: null,
            statusProvider,
            Factory: null));
    }

    public void AddStatusProvider(Func<IServiceProvider, IStatusProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _statusProviders.Add(new PluginCapabilityDescriptor(
            typeof(IStatusProvider),
            ImplementationType: null,
            Instance: null,
            serviceProvider => factory(serviceProvider)));
    }

    public void AddOperation(IOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        _operations.Add(new PluginCapabilityDescriptor(
            typeof(IOperation),
            ImplementationType: null,
            operation,
            Factory: null));
    }

    public void AddOperation(Func<IServiceProvider, IOperation> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _operations.Add(new PluginCapabilityDescriptor(
            typeof(IOperation),
            ImplementationType: null,
            Instance: null,
            serviceProvider => factory(serviceProvider)));
    }
}
