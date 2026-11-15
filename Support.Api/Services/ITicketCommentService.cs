using Support.Api.DTOs;

namespace Support.Api.Services;

public interface ITicketCommentService
{
    Task<TicketCommentResult> CreateAsync(
        Guid ticketId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default);
}
