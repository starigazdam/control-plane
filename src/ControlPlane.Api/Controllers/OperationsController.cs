using ControlPlane.Api.Contracts;
using ControlPlane.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ControlPlane.Api.Controllers;

[ApiController]
[Route("api/operations")]
public sealed class OperationsController : ControllerBase
{
    private readonly ControlPlaneWorkbenchService _workbenchService;

    public OperationsController(ControlPlaneWorkbenchService workbenchService)
    {
        _workbenchService = workbenchService;
    }

    [HttpPost("execute")]
    public async Task<ActionResult<OperationHistoryEntry>> Execute(
        [FromBody] ExecuteOperationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workbenchService.ExecuteAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<OperationHistoryEntry>>> GetHistory(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var history = await _workbenchService.GetExecutionHistoryAsync(take, cancellationToken);
        return Ok(history);
    }
}
