using Microsoft.EntityFrameworkCore;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;
using Support.Api.Services;

namespace Support.UnitTests;

public sealed class TicketCreationServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidRequest_SavesTicketAndReturnsSavedResponse()
    {
        var createdAt = new DateTimeOffset(2026, 2, 20, 14, 30, 0, TimeSpan.Zero);
        await using var context = await CreateContextWithCategoryAsync();
        var service = CreateService(context, createdAt);
        var request = ValidRequest();

        var result = await service.CreateAsync(request);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Ticket);
        Assert.NotEqual(Guid.Empty, result.Ticket.Id);
        Assert.Equal(request.Title, result.Ticket.Title);
        Assert.Equal(request.Description, result.Ticket.Description);
        Assert.Equal(request.CategoryId, result.Ticket.CategoryId);
        Assert.Equal(request.Priority, result.Ticket.Priority);
        Assert.Equal(Status.New, result.Ticket.Status);
        Assert.Equal(createdAt, result.Ticket.CreatedAt);
        Assert.Equal(TimeSpan.Zero, result.Ticket.CreatedAt.Offset);

        var savedTickets = await context.Tickets.ToListAsync();
        var savedTicket = Assert.Single(savedTickets);
        Assert.Equal(result.Ticket.Id, savedTicket.Id);
        Assert.Equal(Status.New, savedTicket.Status);
        Assert.Equal(createdAt, savedTicket.CreatedAt);
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_ReturnsFieldErrorsWithoutSavingTicket()
    {
        await using var context = await CreateContextWithCategoryAsync();
        var service = CreateService(context, DateTimeOffset.UtcNow);
        var request = ValidRequest() with { Title = "   " };

        var result = await service.CreateAsync(request);

        Assert.False(result.IsValid);
        Assert.Null(result.Ticket);
        Assert.Contains(nameof(CreateTicketRequest.Title), result.Errors.Keys);
        Assert.Empty(await context.Tickets.ToListAsync());
    }

    private static ITicketCreationService CreateService(SupportDbContext context, DateTimeOffset createdAt)
    {
        var validator = new CreateTicketRequestValidator(context);
        return new TicketCreationService(context, validator, new FixedClock(createdAt));
    }

    private static CreateTicketRequest ValidRequest() => new()
    {
        Title = "Printer issue",
        Description = "The printer is not working.",
        CategoryId = 1,
        Priority = Priority.High
    };

    private static async Task<SupportDbContext> CreateContextWithCategoryAsync()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new SupportDbContext(options);
        await context.Categories.AddAsync(new Category { Id = 1, Name = "General" });
        await context.SaveChangesAsync();
        return context;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
