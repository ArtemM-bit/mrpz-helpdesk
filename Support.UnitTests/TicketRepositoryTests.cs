using Microsoft.EntityFrameworkCore;
using Support.Api.Data;
using Support.Api.Models;

namespace Support.UnitTests;

public sealed class TicketRepositoryTests
{
    [Fact]
    public async Task GetOverdueAsync_FiltersAndOrdersActiveOverdueTickets()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var now = new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        await using (var context = new SupportDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Tickets.AddRange(
                CreateTicket(firstId, Status.New, now.AddHours(-1)),
                CreateTicket(secondId, Status.InProgress, now.AddHours(-1)),
                CreateTicket(Guid.NewGuid(), Status.Resolved, now.AddHours(-2)),
                CreateTicket(Guid.NewGuid(), Status.Closed, now.AddHours(-3)),
                CreateTicket(Guid.NewGuid(), Status.New, null),
                CreateTicket(Guid.NewGuid(), Status.New, now));
            await context.SaveChangesAsync();
        }

        await using var readContext = new SupportDbContext(options);
        var tickets = await new TicketRepository(readContext).GetOverdueAsync(now);

        Assert.Equal([secondId, firstId], tickets.Select(ticket => ticket.Id).ToArray());
        Assert.All(tickets, ticket =>
        {
            Assert.True(ticket.DueAt.HasValue);
            Assert.True(ticket.DueAt.Value < now);
            Assert.True(ticket.Status is Status.New or Status.InProgress);
        });
    }

    [Fact]
    public async Task GetOverdueAsync_NoMatches_ReturnsEmptyList()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new SupportDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var tickets = await new TicketRepository(context).GetOverdueAsync(DateTimeOffset.UtcNow);

        Assert.Empty(tickets);
    }

    [Fact]
    public async Task AddAsync_TicketCanBeReadFromANewContext()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Заявка",
            Description = "Опис заявки",
            CategoryId = 1,
            Priority = Priority.High,
            Status = Status.New,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using (var writeContext = new SupportDbContext(options))
        {
            await writeContext.Database.EnsureCreatedAsync();
            await new TicketRepository(writeContext).AddAsync(ticket);
        }

        await using var readContext = new SupportDbContext(options);
        var savedTicket = await new TicketRepository(readContext).GetByIdAsync(ticket.Id);

        Assert.NotNull(savedTicket);
        Assert.Equal(ticket.Id, savedTicket.Id);
        Assert.Equal(ticket.Title, savedTicket.Title);
        Assert.Equal(ticket.Description, savedTicket.Description);
        Assert.Equal(ticket.CategoryId, savedTicket.CategoryId);
    }

    private static Ticket CreateTicket(Guid id, Status status, DateTimeOffset? dueAt) => new()
    {
        Id = id,
        Title = "Заявка",
        Description = "Опис заявки",
        CategoryId = 1,
        Priority = Priority.Medium,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        DueAt = dueAt
    };
}
