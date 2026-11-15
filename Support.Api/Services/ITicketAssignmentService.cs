using Support.Api.DTOs;

namespace Support.Api.Services;

public interface ITicketAssignmentService
{
    Task<TicketAssignmentResult> AssignAsync(
        Guid ticketId,
        AssignTicketRequest request,
        CancellationToken cancellationToken = default);
}
