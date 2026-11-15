using Support.Api.DTOs;

namespace Support.Api.Services;

public sealed record CreateTicketResult(
    TicketResponse? Ticket,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
