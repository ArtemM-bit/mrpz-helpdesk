using Support.Api.DTOs;

namespace Support.Api.Services;

public enum TicketStatusError
{
    None,
    InvalidStatus,
    TicketNotFound,
    InvalidTransition,
    AssigneeRequired
}

public sealed record TicketStatusResult(
    TicketResponse? Ticket,
    TicketStatusError Error)
{
    public bool IsSuccessful => Error == TicketStatusError.None;
}
