using Microsoft.AspNetCore.Mvc;
using Support.Api.Data;
using Support.Api.DTOs;

namespace Support.Api.Controllers;

[ApiController]
[Route("api/statistics")]
public sealed class StatisticsController(ITicketRepository ticketRepository) : ControllerBase
{
    /// <summary>Повертає кількість заявок для кожної наявної категорії.</summary>
    /// <remarks>
    /// Кількість охоплює статуси New, InProgress, Resolved і Closed. Категорії без заявок
    /// повертаються з TicketCount = 0. Підрахунок не залежить від фільтрів GET /api/tickets.
    /// </remarks>
    /// <param name="cancellationToken">Токен скасування запиту.</param>
    /// <response code="200">Масив категорій з їхніми ID, назвами та кількістю заявок.</response>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryStatisticsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryStatisticsResponse>>> GetCategories(
        CancellationToken cancellationToken)
    {
        var statistics = await ticketRepository.GetCategoryStatisticsAsync(cancellationToken);
        return Ok(statistics);
    }
}
