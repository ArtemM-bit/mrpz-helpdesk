using Support.Api.Data;
using Support.Api.DTOs;

namespace Support.Api.Services;

public sealed class TicketCreationService(
    ITicketRepository ticketRepository,
    CreateTicketRequestValidator validator,
    IClock clock) : ITicketCreationService
{
    public async Task<CreateTicketResult> CreateAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return new CreateTicketResult(null, validation.Errors);
        }

        var ticket = validation.NormalizedRequest!.ToTicket(clock.UtcNow.ToUniversalTime());
        await ticketRepository.AddAsync(ticket, cancellationToken);

        return new CreateTicketResult(ticket.ToResponse(), new Dictionary<string, string[]>());
    }
}
