using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Operations;

namespace ControlPlane.DevOps.Operations;

public sealed class RerunFailedPipelineOperation : IOperation
{
    public OperationDefinition Definition => new(
        Id: "rerun-failed-pipeline",
        DisplayName: "Rerun Failed Pipeline",
        Description: "Reruns a failed Azure DevOps pipeline for the selected project.",
        RequiresConfirmation: true,
        Parameters:
        [
            new OperationParameter(
                Name: "pipelineId",
                DisplayName: "Pipeline ID",
                Description: "The failed pipeline identifier.",
                IsRequired: true,
                Type: "string",
                DefaultValue: null)
        ]);

    public Task<IReadOnlyList<string>> ValidateAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var errors = new List<string>();
        if (!context.Request.Input.TryGetValue("pipelineId", out var pipelineId) || string.IsNullOrWhiteSpace(pipelineId))
        {
            errors.Add("The 'pipelineId' parameter is required.");
        }

        return Task.FromResult<IReadOnlyList<string>>(errors);
    }

    public Task<OperationExecutionResult> ExecuteAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startedAtUtc = DateTimeOffset.UtcNow;
        var pipelineId = context.Request.Input["pipelineId"]!;
        var output = new Dictionary<string, string?>
        {
            ["pipelineId"] = pipelineId,
            ["executionMode"] = "stub",
            ["note"] = "Wire this operation to Azure DevOps run pipeline API."
        };

        return Task.FromResult(
            new OperationExecutionResult(
                Status: OperationExecutionStatus.Succeeded,
                Message: $"Rerun request accepted for pipeline '{pipelineId}'.",
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                ErrorCode: null,
                Output: output));
    }
}
