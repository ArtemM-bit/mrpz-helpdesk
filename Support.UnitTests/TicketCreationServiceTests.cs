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
        var repository = new FakeTicketRepository();
        var service = CreateService(context, repository, createdAt);
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

        var savedTicket = Assert.Single(repository.AddedTickets);
        Assert.Equal(result.Ticket.Id, savedTicket.Id);
        Assert.Equal(Status.New, savedTicket.Status);
        Assert.Equal(createdAt, savedTicket.CreatedAt);
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_ReturnsFieldErrorsWithoutSavingTicket()
    {
        await using var context = await CreateContextWithCategoryAsync();
        var repository = new FakeTicketRepository();
        var service = CreateService(context, repository, new DateTimeOffset(2026, 2, 20, 14, 30, 0, TimeSpan.Zero));
        var request = ValidRequest() with { Title = "   " };

        var result = await service.CreateAsync(request);

        Assert.False(result.IsValid);
        Assert.Null(result.Ticket);
        Assert.Contains(nameof(CreateTicketRequest.Title), result.Errors.Keys);
        Assert.Empty(repository.AddedTickets);
    }

    private static ITicketCreationService CreateService(
        SupportDbContext context,
        ITicketRepository repository,
        DateTimeOffset createdAt)
    {
        var validator = new CreateTicketRequestValidator(context);
        return new TicketCreationService(repository, validator, new FixedClock(createdAt));
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
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeTicketRepository : ITicketRepository
    {
        public List<Ticket> AddedTickets { get; } = [];

        public Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default)
        {
            AddedTickets.Add(ticket);
            return Task.CompletedTask;
        }

        public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AddedTickets.SingleOrDefault(ticket => ticket.Id == id));
        }

        public Task<Ticket?> GetForAssignmentAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Ticket?>(null);

        public Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
