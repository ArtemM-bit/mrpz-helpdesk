using Support.Api.Models;

namespace Support.Api.DTOs;

public static class TicketMappings
{
    public static Ticket ToTicket(this CreateTicketRequest request, DateTimeOffset createdAt)
    {
        return new Ticket
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            CategoryId = request.CategoryId,
            Priority = request.Priority,
            Status = Status.New,
            CreatedAt = createdAt.ToUniversalTime(),
            DueAt = null
        };
    }

    public static TicketResponse ToResponse(this Ticket ticket, DateTimeOffset? currentTime = null)
    {
        var dueAt = ticket.DueAt?.ToUniversalTime();
        var now = (currentTime ?? DateTimeOffset.UtcNow).ToUniversalTime();

        return new TicketResponse(
            ticket.Id,
            ticket.Title,
            ticket.Description,
            ticket.CategoryId,
            ticket.Priority,
            ticket.Status,
            ticket.CreatedAt.ToUniversalTime(),
            ticket.AssigneeId,
            ticket.ResolvedAt?.ToUniversalTime(),
            dueAt,
            TicketOverdueRules.IsOverdue(ticket, now));
    }
}
