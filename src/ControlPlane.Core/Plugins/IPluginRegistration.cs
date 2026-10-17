using ControlPlane.Core.Interfaces;

namespace ControlPlane.Core.Plugins;

public interface IPluginRegistration
{
    void AddStatusProvider<TStatusProvider>() where TStatusProvider : class, IStatusProvider;

    void AddOperation<TOperation>() where TOperation : class, IOperation;

    void AddStatusProvider(IStatusProvider statusProvider);

    void AddStatusProvider(Func<IServiceProvider, IStatusProvider> factory);

    void AddOperation(IOperation operation);

    void AddOperation(Func<IServiceProvider, IOperation> factory);
}
