using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;
using Support.Api.Services;

namespace Support.UnitTests;

public sealed class TicketDeadlineServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("2026-11-21T12:01Z")]
    [InlineData("2026-11-21T12:01:00Z")]
    [InlineData("2026-11-21T14:01:00+02:00")]
    public async Task UpdateAsync_FutureDueAt_StoresUtcAndReturnsResponse(string dueAt)
    {
        var ticket = CreateTicket();
        var repository = new FakeTicketRepository(ticket);
        var service = new TicketDeadlineService(repository, new FixedClock(Now));

        var result = await service.UpdateAsync(ticket.Id, new UpdateDeadlineRequest { DueAt = dueAt });

        Assert.True(result.IsSuccessful);
        Assert.Equal(TicketDeadlineError.None, result.Error);
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 12, 1, 0, TimeSpan.Zero), ticket.DueAt);
        Assert.Equal(ticket.DueAt, result.Ticket!.DueAt);
        Assert.False(result.Ticket.IsOverdue);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Theory]
    [InlineData("2026-11-21T12:00:00Z")]
    [InlineData("2026-11-21T13:00:00+01:00")]
    [InlineData("2026-11-21T11:59:59Z")]
    public async Task UpdateAsync_PastOrCurrentDueAt_ReturnsDueAtErrorWithoutSaving(string dueAt)
    {
        await AssertInvalidDueAtAsync(dueAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tomorrow")]
    [InlineData("2026-11-21T12:01:00")]
    [InlineData("2026-11-21 12:01:00+00:00")]
    public async Task UpdateAsync_MissingOrInvalidDueAt_ReturnsDueAtErrorWithoutSaving(string? dueAt)
    {
        await AssertInvalidDueAtAsync(dueAt);
    }

    [Theory]
    [InlineData("2026-11-21T15:30:00+03:00")]
    [InlineData("2026-11-21T07:30:00-05:00")]
    [InlineData("2026-11-21T12:30:00Z")]
    public async Task UpdateAsync_EquivalentOffsets_ProduceSameUtcDueAt(string dueAt)
    {
        var ticket = CreateTicket();
        var service = new TicketDeadlineService(new FakeTicketRepository(ticket), new FixedClock(Now));

        var result = await service.UpdateAsync(ticket.Id, new UpdateDeadlineRequest { DueAt = dueAt });

        Assert.True(result.IsSuccessful);
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 12, 30, 0, TimeSpan.Zero), result.Ticket!.DueAt);
    }

    [Theory]
    [InlineData(Status.Resolved)]
    [InlineData(Status.Closed)]
    public async Task UpdateAsync_TerminalStatus_ReturnsConflictWithoutChangingDueAt(Status status)
    {
        var ticket = CreateTicket();
        ticket.Status = status;
        var originalDueAt = Now.AddDays(-2);
        ticket.DueAt = originalDueAt;
        var repository = new FakeTicketRepository(ticket);
        var service = new TicketDeadlineService(repository, new FixedClock(Now));

        var result = await service.UpdateAsync(
            ticket.Id,
            new UpdateDeadlineRequest { DueAt = "2026-11-22T12:00:00Z" });

        Assert.False(result.IsSuccessful);
        Assert.Equal(TicketDeadlineError.InvalidStatus, result.Error);
        Assert.Equal(originalDueAt, ticket.DueAt);
        Assert.Equal(status, ticket.Status);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_UnknownTicket_ReturnsNotFoundWithoutSaving()
    {
        var repository = new FakeTicketRepository(null);
        var service = new TicketDeadlineService(repository, new FixedClock(Now));

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateDeadlineRequest { DueAt = "2026-11-22T12:00:00Z" });

        Assert.False(result.IsSuccessful);
        Assert.Equal(TicketDeadlineError.TicketNotFound, result.Error);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_RepeatedDeadline_StoresReplacementAndClearsOverdue()
    {
        var ticket = CreateTicket();
        ticket.Status = Status.InProgress;
        ticket.DueAt = Now.AddDays(-1);
        var repository = new FakeTicketRepository(ticket);
        var service = new TicketDeadlineService(repository, new FixedClock(Now));

        var firstResult = await service.UpdateAsync(
            ticket.Id,
            new UpdateDeadlineRequest { DueAt = "2026-11-22T12:00:00Z" });
        var replacement = await service.UpdateAsync(
            ticket.Id,
            new UpdateDeadlineRequest { DueAt = "2026-11-23T12:00:00Z" });

        Assert.True(firstResult.IsSuccessful);
        Assert.True(replacement.IsSuccessful);
        Assert.Equal(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero), ticket.DueAt);
        Assert.False(replacement.Ticket!.IsOverdue);
        Assert.Equal(Status.InProgress, ticket.Status);
        Assert.Equal(2, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task UpdateAsync_RequiresDueAtField()
    {
        var property = typeof(UpdateDeadlineRequest).GetProperty(nameof(UpdateDeadlineRequest.DueAt));

        Assert.NotNull(property);
        Assert.True(Attribute.IsDefined(property!, typeof(System.ComponentModel.DataAnnotations.RequiredAttribute)));

        await AssertInvalidDueAtAsync(null);
    }

    private static async Task AssertInvalidDueAtAsync(string? dueAt)
    {
        var ticket = CreateTicket();
        var originalDueAt = Now.AddDays(1);
        ticket.DueAt = originalDueAt;
        var repository = new FakeTicketRepository(ticket);
        var service = new TicketDeadlineService(repository, new FixedClock(Now));

        var result = await service.UpdateAsync(ticket.Id, new UpdateDeadlineRequest { DueAt = dueAt });

        Assert.False(result.IsSuccessful);
        Assert.Equal(TicketDeadlineError.InvalidDueAt, result.Error);
        Assert.True(result.Errors.TryGetValue(nameof(UpdateDeadlineRequest.DueAt), out var messages));
        Assert.NotEmpty(messages!);
        Assert.Equal(originalDueAt, ticket.DueAt);
        Assert.Equal(0, repository.SaveChangesCalls);
        Assert.Equal(1, repository.GetForAssignmentCalls);
    }

    private static Ticket CreateTicket() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Заявка",
        Description = "Опис",
        CategoryId = 1,
        Priority = Priority.Medium,
        Status = Status.New,
        CreatedAt = Now.AddDays(-1)
    };

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeTicketRepository(Ticket? ticket) : ITicketRepository
    {
        public int GetForAssignmentCalls { get; private set; }

        public int SaveChangesCalls { get; private set; }

        public Task AddAsync(Ticket addedTicket, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Ticket?>(ticket?.Id == id ? ticket : null);

        public Task<Ticket?> GetForAssignmentAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetForAssignmentCalls++;
            return Task.FromResult<Ticket?>(ticket?.Id == id ? ticket : null);
        }

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
            SaveChangesCalls++;
            return Task.CompletedTask;
        }
    }
}
