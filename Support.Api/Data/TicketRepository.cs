using Microsoft.EntityFrameworkCore;
using Support.Api.DTOs;
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

    public async Task<IReadOnlyList<Ticket>> SearchAsync(
        TicketSearchRequest search,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Ticket> query = dbContext.Tickets.AsNoTracking();

        if (search.Status.HasValue)
        {
            query = query.Where(ticket => ticket.Status == search.Status.Value);
        }

        if (search.Priority.HasValue)
        {
            query = query.Where(ticket => ticket.Priority == search.Priority.Value);
        }

        if (search.CategoryId.HasValue)
        {
            query = query.Where(ticket => ticket.CategoryId == search.CategoryId.Value);
        }

        var sortDirection = search.SortDirection?.Trim();
        query = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(ticket => ticket.CreatedAt).ThenBy(ticket => ticket.Id)
            : query.OrderByDescending(ticket => ticket.CreatedAt).ThenBy(ticket => ticket.Id);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryStatisticsResponse>> GetCategoryStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        var ticketCounts = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Status == Status.New
                || ticket.Status == Status.InProgress
                || ticket.Status == Status.Resolved
                || ticket.Status == Status.Closed)
            .GroupBy(ticket => ticket.CategoryId)
            .Select(group => new { CategoryId = group.Key, TicketCount = group.Count() })
            .ToDictionaryAsync(group => group.CategoryId, group => group.TicketCount, cancellationToken);

        var categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.Id)
            .Select(category => new { category.Id, category.Name })
            .ToListAsync(cancellationToken);

        return categories
            .Select(category => new CategoryStatisticsResponse(
                category.Id,
                category.Name,
                ticketCounts.GetValueOrDefault(category.Id)))
            .ToList();
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
