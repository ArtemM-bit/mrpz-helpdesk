using Support.Api.DTOs;

namespace Support.Api.Services;

public interface ITicketDeadlineService
{
    Task<TicketDeadlineResult> UpdateAsync(
        Guid ticketId,
        UpdateDeadlineRequest request,
        CancellationToken cancellationToken = default);
}
