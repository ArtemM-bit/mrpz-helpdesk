using Support.Api.DTOs;

namespace Support.Api.Services;

public enum TicketAssignmentError
{
    None,
    TicketNotFound,
    InvalidAssignee,
    TicketClosed
}

public sealed record TicketAssignmentResult(
    TicketResponse? Ticket,
    TicketAssignmentError Error)
{
    public bool IsSuccessful => Error == TicketAssignmentError.None;
}
