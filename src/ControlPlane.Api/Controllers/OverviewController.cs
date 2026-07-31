using ControlPlane.Api.Contracts;
using ControlPlane.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ControlPlane.Api.Controllers;

[ApiController]
[Route("api/overview")]
public sealed class OverviewController : ControllerBase
{
    private readonly ControlPlaneWorkbenchService _workbenchService;

    public OverviewController(ControlPlaneWorkbenchService workbenchService)
    {
        _workbenchService = workbenchService;
    }

    [HttpGet]
    public async Task<ActionResult<OverviewResponse>> Get(CancellationToken cancellationToken)
    {
        var response = await _workbenchService.GetOverviewAsync(cancellationToken);
        return Ok(response);
    }
}
