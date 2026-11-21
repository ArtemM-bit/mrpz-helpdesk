using Support.Api.DTOs;
using Support.Api.Models;

namespace Support.Api.Data;

public interface ITicketRepository
{
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default);

    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Ticket?> GetForAssignmentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ticket>> SearchAsync(
        TicketSearchRequest search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryStatisticsResponse>> GetCategoryStatisticsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ticket>> GetOverdueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Ticket>>([]);

    Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);

    Task AddCommentAsync(TicketComment comment, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TicketComment>> GetCommentsByTicketIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
