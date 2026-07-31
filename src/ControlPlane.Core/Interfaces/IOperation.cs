using ControlPlane.Core.Operations;

namespace ControlPlane.Core.Interfaces;

public interface IOperation
{
    OperationDefinition Definition { get; }

    Task<IReadOnlyList<string>> ValidateAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken);

    Task<OperationExecutionResult> ExecuteAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken);
}
