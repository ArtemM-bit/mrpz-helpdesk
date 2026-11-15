using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;

namespace Support.Api.Services;

public sealed class TicketAssignmentService(ITicketRepository ticketRepository) : ITicketAssignmentService
{
    public async Task<TicketAssignmentResult> AssignAsync(
        Guid ticketId,
        AssignTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticket = await ticketRepository.GetForAssignmentAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return new TicketAssignmentResult(null, TicketAssignmentError.TicketNotFound);
        }

        var assignee = await ticketRepository.GetUserByIdAsync(request.AssigneeId, cancellationToken);
        if (assignee is null || !assignee.IsSupportEmployee)
        {
            return new TicketAssignmentResult(null, TicketAssignmentError.InvalidAssignee);
        }

        if (ticket.Status == Status.Closed)
        {
            return new TicketAssignmentResult(null, TicketAssignmentError.TicketClosed);
        }

        ticket.AssigneeId = assignee.Id;
        await ticketRepository.SaveChangesAsync(cancellationToken);

        return new TicketAssignmentResult(ticket.ToResponse(), TicketAssignmentError.None);
    }
}
