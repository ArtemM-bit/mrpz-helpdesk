using Microsoft.AspNetCore.Mvc;
using Support.Api.Data;
using Support.Api.DTOs;

namespace Support.Api.Controllers;

[ApiController]
[Route("api/statistics")]
public sealed class StatisticsController(ITicketRepository ticketRepository) : ControllerBase
{
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryStatisticsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryStatisticsResponse>>> GetCategories(
        CancellationToken cancellationToken)
    {
        var statistics = await ticketRepository.GetCategoryStatisticsAsync(cancellationToken);
        return Ok(statistics);
    }
}
