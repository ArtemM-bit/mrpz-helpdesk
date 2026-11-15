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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
