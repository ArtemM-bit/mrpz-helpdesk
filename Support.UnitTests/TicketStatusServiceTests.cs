using Microsoft.EntityFrameworkCore;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;
using Support.Api.Services;

namespace Support.UnitTests;

public sealed class TicketStatusServiceTests
{
    private static readonly DateTimeOffset ResolvedAt = new(2026, 11, 20, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ChangeStatusAsync_AllowedTransitions_PersistStatusAndResolvedAt()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var ticket = CreateTicket(Status.New, assigneeId: 7);

        await using (var setupContext = new SupportDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Tickets.Add(ticket);
            await setupContext.SaveChangesAsync();
        }

        TicketStatusResult inProgress;
        TicketStatusResult resolved;
        TicketStatusResult closed;
        await using (var updateContext = new SupportDbContext(options))
        {
            var service = new TicketStatusService(new TicketRepository(updateContext), new FixedClock(ResolvedAt));
            var ticketId = ticket.Id;

            inProgress = await service.ChangeStatusAsync(ticketId, new ChangeTicketStatusRequest(Status.InProgress));
            resolved = await service.ChangeStatusAsync(ticketId, new ChangeTicketStatusRequest(Status.Resolved));
            closed = await service.ChangeStatusAsync(ticketId, new ChangeTicketStatusRequest(Status.Closed));
        }

        await using var readContext = new SupportDbContext(options);
        var savedTicket = await new TicketRepository(readContext).GetByIdAsync(ticket.Id);

        Assert.True(inProgress.IsSuccessful);
        Assert.Equal(Status.InProgress, inProgress.Ticket?.Status);
        Assert.Null(inProgress.Ticket?.ResolvedAt);
        Assert.True(resolved.IsSuccessful);
        Assert.Equal(Status.Resolved, resolved.Ticket?.Status);
        Assert.Equal(ResolvedAt, resolved.Ticket?.ResolvedAt);
        Assert.True(closed.IsSuccessful);
        Assert.Equal(Status.Closed, closed.Ticket?.Status);
        Assert.Equal(ResolvedAt, closed.Ticket?.ResolvedAt);
        Assert.NotNull(savedTicket);
        Assert.Equal(Status.Closed, savedTicket.Status);
        Assert.Equal(ResolvedAt, savedTicket.ResolvedAt);
    }

    [Theory]
    [InlineData(Status.New, Status.New, TicketStatusError.InvalidTransition)]
    [InlineData(Status.InProgress, Status.InProgress, TicketStatusError.InvalidTransition)]
    [InlineData(Status.Resolved, Status.Resolved, TicketStatusError.InvalidTransition)]
    [InlineData(Status.Closed, Status.New, TicketStatusError.InvalidTransition)]
    [InlineData(Status.Closed, Status.InProgress, TicketStatusError.InvalidTransition)]
    [InlineData(Status.Closed, Status.Resolved, TicketStatusError.InvalidTransition)]
    [InlineData(Status.New, Status.InProgress, TicketStatusError.AssigneeRequired)]
    public async Task ChangeStatusAsync_RejectedTransition_DoesNotChangeOrSave(
        Status currentStatus,
        Status targetStatus,
        TicketStatusError expectedError)
    {
        var originalResolvedAt = ResolvedAt.AddDays(-1);
        var ticket = CreateTicket(currentStatus, assigneeId: currentStatus == Status.New ? null : 7);
        ticket.ResolvedAt = originalResolvedAt;
        var repository = new FakeTicketRepository(ticket);
        var service = new TicketStatusService(repository, new FixedClock(ResolvedAt));

        var result = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest(targetStatus));

        Assert.Equal(expectedError, result.Error);
        Assert.Equal(currentStatus, ticket.Status);
        Assert.Equal(originalResolvedAt, ticket.ResolvedAt);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task ChangeStatusAsync_UnknownStatus_DoesNotChangeOrSave()
    {
        var ticket = CreateTicket(Status.InProgress, assigneeId: 7);
        ticket.ResolvedAt = ResolvedAt.AddDays(-1);
        var repository = new FakeTicketRepository(ticket);
        var service = new TicketStatusService(repository, new FixedClock(ResolvedAt));

        var result = await service.ChangeStatusAsync(ticket.Id, new ChangeTicketStatusRequest((Status)999));

        Assert.Equal(TicketStatusError.InvalidStatus, result.Error);
        Assert.Equal(Status.InProgress, ticket.Status);
        Assert.Equal(ResolvedAt.AddDays(-1), ticket.ResolvedAt);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task ChangeStatusAsync_MissingTicket_ReturnsNotFoundWithoutSaving()
    {
        var repository = new FakeTicketRepository(CreateTicket(Status.New, assigneeId: 7));
        var service = new TicketStatusService(repository, new FixedClock(ResolvedAt));

        var result = await service.ChangeStatusAsync(Guid.NewGuid(), new ChangeTicketStatusRequest(Status.InProgress));

        Assert.Equal(TicketStatusError.TicketNotFound, result.Error);
        Assert.Equal(0, repository.SaveCount);
    }

    private static Ticket CreateTicket(Status status, int? assigneeId) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Заявка",
        Description = "Опис заявки",
        CategoryId = 1,
        Priority = Priority.High,
        Status = status,
        CreatedAt = ResolvedAt.AddDays(-3),
        AssigneeId = assigneeId
    };

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeTicketRepository(Ticket ticket) : ITicketRepository
    {
        public int SaveCount { get; private set; }

        public Task AddAsync(Ticket value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Ticket?>(ticket.Id == id ? ticket : null);

        public Task<Ticket?> GetForAssignmentAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Ticket?>(ticket.Id == id ? ticket : null);

        public Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task AddCommentAsync(TicketComment comment, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<TicketComment>> GetCommentsByTicketIdAsync(
            Guid ticketId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TicketComment>>([]);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
