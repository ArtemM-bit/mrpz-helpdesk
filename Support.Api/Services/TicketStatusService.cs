using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;

namespace Support.Api.Services;

public sealed class TicketStatusService(ITicketRepository ticketRepository, IClock clock) : ITicketStatusService
{
    public async Task<TicketStatusResult> ChangeStatusAsync(
        Guid ticketId,
        ChangeTicketStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Status))
        {
            return new TicketStatusResult(null, TicketStatusError.InvalidStatus);
        }

        var ticket = await ticketRepository.GetForAssignmentAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return new TicketStatusResult(null, TicketStatusError.TicketNotFound);
        }

        var isAllowedTransition = (ticket.Status, request.Status) is
            (Status.New, Status.InProgress) or
            (Status.InProgress, Status.Resolved) or
            (Status.Resolved, Status.Closed);

        if (!isAllowedTransition)
        {
            return new TicketStatusResult(null, TicketStatusError.InvalidTransition);
        }

        if (ticket.Status == Status.New && ticket.AssigneeId is null)
        {
            return new TicketStatusResult(null, TicketStatusError.AssigneeRequired);
        }

        ticket.Status = request.Status;
        if (request.Status == Status.Resolved)
        {
            ticket.ResolvedAt = clock.UtcNow;
        }

        await ticketRepository.SaveChangesAsync(cancellationToken);

        return new TicketStatusResult(ticket.ToResponse(), TicketStatusError.None);
    }
}
