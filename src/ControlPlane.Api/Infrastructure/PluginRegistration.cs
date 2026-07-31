using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Plugins;

namespace ControlPlane.Api.Infrastructure;

internal sealed class PluginRegistration : IPluginRegistration
{
    private readonly HashSet<Type> _statusProviderTypes = [];
    private readonly HashSet<Type> _operationTypes = [];

    public IReadOnlyList<Type> StatusProviderTypes => _statusProviderTypes.ToArray();

    public IReadOnlyList<Type> OperationTypes => _operationTypes.ToArray();

    public void AddStatusProvider<TStatusProvider>() where TStatusProvider : class, IStatusProvider
    {
        _statusProviderTypes.Add(typeof(TStatusProvider));
    }

    public void AddOperation<TOperation>() where TOperation : class, IOperation
    {
        _operationTypes.Add(typeof(TOperation));
    }
}
