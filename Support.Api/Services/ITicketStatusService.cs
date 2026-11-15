using Support.Api.DTOs;
using Support.Api.Models;

namespace Support.Api.Services;

public interface ITicketStatusService
{
    Task<TicketStatusResult> ChangeStatusAsync(
        Guid ticketId,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken = default);
}
