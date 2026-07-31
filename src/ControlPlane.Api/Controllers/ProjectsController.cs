using ControlPlane.Api.Contracts;
using ControlPlane.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ControlPlane.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly ControlPlaneWorkbenchService _workbenchService;

    public ProjectsController(ControlPlaneWorkbenchService workbenchService)
    {
        _workbenchService = workbenchService;
    }

    [HttpGet("{projectId}")]
    public async Task<ActionResult<ProjectDetailsResponse>> GetById(
        string projectId,
        CancellationToken cancellationToken)
    {
        var response = await _workbenchService.GetProjectAsync(projectId, cancellationToken);
        if (response is null)
        {
            return NotFound();
        }

        return Ok(response);
    }
}
