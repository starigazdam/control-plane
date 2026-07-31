using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Operations;

namespace ControlPlane.Azure.Operations;

public sealed class RestartAppServiceOperation : IOperation
{
    public OperationDefinition Definition => new(
        Id: "restart-app-service",
        DisplayName: "Restart App Service",
        Description: "Restarts the target Azure App Service for the selected project.",
        RequiresConfirmation: true,
        Parameters:
        [
            new OperationParameter(
                Name: "serviceName",
                DisplayName: "Service Name",
                Description: "Azure App Service resource name.",
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
        if (!context.Request.Input.TryGetValue("serviceName", out var serviceName) || string.IsNullOrWhiteSpace(serviceName))
        {
            errors.Add("The 'serviceName' parameter is required.");
        }

        return Task.FromResult<IReadOnlyList<string>>(errors);
    }

    public Task<OperationExecutionResult> ExecuteAsync(
        OperationExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startedAtUtc = DateTimeOffset.UtcNow;
        var serviceName = context.Request.Input["serviceName"]!;
        var output = new Dictionary<string, string?>
        {
            ["serviceName"] = serviceName,
            ["executionMode"] = "stub",
            ["note"] = "Wire this operation to Azure SDK/App Service restart command."
        };

        return Task.FromResult(
            new OperationExecutionResult(
                Status: OperationExecutionStatus.Succeeded,
                Message: $"Restart request accepted for App Service '{serviceName}'.",
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow,
                ErrorCode: null,
                Output: output));
    }
}
