using Microsoft.EntityFrameworkCore;
using Support.Api.Data;
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
}
