using Support.Api.DTOs;

namespace Support.Api.Services;

public interface ITicketCreationService
{
    Task<CreateTicketResult> CreateAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken = default);
}
