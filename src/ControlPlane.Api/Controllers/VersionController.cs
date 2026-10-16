using ControlPlane.Api.Configuration;
using ControlPlane.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ControlPlane.Api.Controllers;

[ApiController]
[Route("api/version")]
public sealed class VersionController : ControllerBase
{
    private readonly BuildSettings _buildSettings;

    public VersionController(IOptions<BuildSettings> buildSettings)
    {
        _buildSettings = buildSettings.Value;
    }

    [HttpGet]
    public ActionResult<VersionResponse> Get()
    {
        return Ok(new VersionResponse(_buildSettings.ResolveCommitSha()));
    }
}
