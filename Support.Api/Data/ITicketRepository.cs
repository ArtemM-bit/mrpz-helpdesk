using Support.Api.Models;

namespace Support.Api.Data;

public interface ITicketRepository
{
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default);

    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Ticket?> GetForAssignmentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
