using Microsoft.AspNetCore.Mvc;
using Support.Api.DTOs;
using Support.Api.Services;

namespace Support.Api.Controllers;

[ApiController]
[Route("api/status")]
public sealed class StatusController(IStatusService statusService) : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiStatusResponse> Get()
    {
        var status = statusService.GetStatus();
        return Ok(new ApiStatusResponse(status.Name, status.Environment, status.CheckedAtUtc));
    }
}
