using Support.Api.DTOs;

namespace Support.Api.Services;

public enum TicketDeadlineError
{
    None,
    InvalidDueAt,
    TicketNotFound
}

public sealed record TicketDeadlineResult(
    TicketResponse? Ticket,
    TicketDeadlineError Error,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsSuccessful => Error == TicketDeadlineError.None;
}
