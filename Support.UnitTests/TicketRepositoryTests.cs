using Microsoft.EntityFrameworkCore;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;

namespace Support.UnitTests;

public sealed class TicketRepositoryTests
{
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

    [Fact]
    public async Task SearchAsync_FiltersAndSortsDeterministically()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var createdAt = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var sameDateTicketId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var lowerIdTicketId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var newerTicketId = Guid.Parse("00000000-0000-0000-0000-000000000003");

        await using (var context = new SupportDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Tickets.AddRange(
                new Ticket
                {
                    Id = sameDateTicketId,
                    Title = "Друга",
                    Description = "Опис",
                    CategoryId = 1,
                    Priority = Priority.High,
                    Status = Status.New,
                    CreatedAt = createdAt
                },
                new Ticket
                {
                    Id = lowerIdTicketId,
                    Title = "Перша",
                    Description = "Опис",
                    CategoryId = 1,
                    Priority = Priority.High,
                    Status = Status.New,
                    CreatedAt = createdAt
                },
                new Ticket
                {
                    Id = newerTicketId,
                    Title = "Новіша",
                    Description = "Опис",
                    CategoryId = 1,
                    Priority = Priority.High,
                    Status = Status.New,
                    CreatedAt = createdAt.AddHours(1)
                });
            await context.SaveChangesAsync();
        }

        await using var readContext = new SupportDbContext(options);
        var tickets = await new TicketRepository(readContext).SearchAsync(
            new TicketSearchRequest(Status.New, Priority.High, 1, "asc"));

        Assert.Equal(3, tickets.Count);
        Assert.Equal([lowerIdTicketId, sameDateTicketId, newerTicketId], tickets.Select(ticket => ticket.Id));

        var descending = await new TicketRepository(readContext).SearchAsync(
            new TicketSearchRequest(Status.New, Priority.High, 1, "desc"));

        Assert.Equal(
            createdAt.AddHours(1),
            descending.First().CreatedAt);
        Assert.Equal(newerTicketId, descending.First().Id);
        Assert.Equal(lowerIdTicketId, descending.Skip(1).First().Id);
        Assert.Equal(sameDateTicketId, descending.Skip(2).First().Id);

        var noMatches = await new TicketRepository(readContext).SearchAsync(
            new TicketSearchRequest(Status.Closed, Priority.Critical, 999, "asc"));

        Assert.Empty(noMatches);
    }
}
