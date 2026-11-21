namespace Support.Api.Models;

public static class TicketOverdueRules
{
    public static bool IsOverdue(Ticket ticket, DateTimeOffset now)
    {
        return ticket.DueAt.HasValue
            && ticket.DueAt.Value.ToUniversalTime() < now.ToUniversalTime()
            && ticket.Status is Status.New or Status.InProgress;
    }
}
