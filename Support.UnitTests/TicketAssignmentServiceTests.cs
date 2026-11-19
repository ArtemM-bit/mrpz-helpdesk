using Microsoft.EntityFrameworkCore;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;
using Support.Api.Services;

namespace Support.UnitTests;

public sealed class TicketAssignmentServiceTests
{
    [Fact]
    public async Task AssignAsync_ValidRequest_ReplacesAssigneeAndPreservesStatusAfterReload()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = CreateOptions(databaseName);
        var ticket = CreateTicket(Status.InProgress, assigneeId: 2);

        await using (var setupContext = new SupportDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Users.Add(new User { Id = 3, FullName = "Працівник підтримки", IsSupportEmployee = true });
            setupContext.Tickets.Add(ticket);
            await setupContext.SaveChangesAsync();
        }

        TicketAssignmentResult result;
        await using (var assignmentContext = new SupportDbContext(options))
        {
            var service = new TicketAssignmentService(new TicketRepository(assignmentContext));
            result = await service.AssignAsync(ticket.Id, new AssignTicketRequest(3));
        }

        await using var readContext = new SupportDbContext(options);
        var savedTicket = await new TicketRepository(readContext).GetByIdAsync(ticket.Id);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Ticket);
        Assert.Equal(3, result.Ticket.AssigneeId);
        Assert.Equal(Status.InProgress, result.Ticket.Status);
        Assert.NotNull(savedTicket);
        Assert.Equal(3, savedTicket.AssigneeId);
        Assert.Equal(Status.InProgress, savedTicket.Status);
    }

    [Fact]
    public async Task AssignAsync_MissingTicket_DoesNotSave()
    {
        var ticket = CreateTicket(Status.New, assigneeId: 2);
        var repository = new FakeTicketRepository(ticket, new User { Id = 2, IsSupportEmployee = true });
        var service = new TicketAssignmentService(repository);

        var result = await service.AssignAsync(Guid.NewGuid(), new AssignTicketRequest(2));

        Assert.Equal(TicketAssignmentError.TicketNotFound, result.Error);
        Assert.Equal(2, ticket.AssigneeId);
        Assert.Equal(0, repository.SaveCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task AssignAsync_InvalidAssignee_DoesNotChangeOrSave(bool userExists, bool isSupportEmployee)
    {
        var ticket = CreateTicket(Status.InProgress, assigneeId: 2);
        var assignee = userExists
            ? new User { Id = 3, IsSupportEmployee = isSupportEmployee }
            : null;
        var repository = new FakeTicketRepository(ticket, assignee);
        var service = new TicketAssignmentService(repository);

        var result = await service.AssignAsync(ticket.Id, new AssignTicketRequest(3));

        Assert.Equal(TicketAssignmentError.InvalidAssignee, result.Error);
        Assert.Null(result.Ticket);
        Assert.Equal(2, ticket.AssigneeId);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task AssignAsync_ClosedTicket_DoesNotChangeOrSave()
    {
        var ticket = CreateTicket(Status.Closed, assigneeId: 2);
        var repository = new FakeTicketRepository(ticket, new User { Id = 3, IsSupportEmployee = true });
        var service = new TicketAssignmentService(repository);

        var result = await service.AssignAsync(ticket.Id, new AssignTicketRequest(3));

        Assert.Equal(TicketAssignmentError.TicketClosed, result.Error);
        Assert.Null(result.Ticket);
        Assert.Equal(2, ticket.AssigneeId);
        Assert.Equal(Status.Closed, ticket.Status);
        Assert.Equal(0, repository.SaveCount);
    }

    private static DbContextOptions<SupportDbContext> CreateOptions(string databaseName) =>
        new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

    private static Ticket CreateTicket(Status status, int assigneeId) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Заявка",
        Description = "Опис заявки",
        CategoryId = 1,
        Priority = Priority.High,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        AssigneeId = assigneeId
    };

    private sealed class FakeTicketRepository(Ticket ticket, User? user) : ITicketRepository
    {
        public int SaveCount { get; private set; }

        public Task AddAsync(Ticket value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Ticket?>(ticket.Id == id ? ticket : null);

        public Task<Ticket?> GetForAssignmentAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Ticket?>(ticket.Id == id ? ticket : null);

        public Task<IReadOnlyList<CategoryStatisticsResponse>> GetCategoryStatisticsAsync(
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user?.Id == id ? user : null);

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
