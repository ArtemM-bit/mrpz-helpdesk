using Microsoft.EntityFrameworkCore;
using Support.Api.Models;

namespace Support.Api.Data;

public sealed class TicketRepository(SupportDbContext dbContext) : ITicketRepository
{
    public async Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        await dbContext.Tickets.AddAsync(ticket, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);
    }

    public Task<Ticket?> GetForAssignmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Tickets
            .SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);
    }

    public Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public async Task AddCommentAsync(TicketComment comment, CancellationToken cancellationToken = default)
    {
        await dbContext.TicketComments.AddAsync(comment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TicketComment>> GetCommentsByTicketIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.TicketComments
            .AsNoTracking()
            .Where(comment => comment.TicketId == ticketId)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
