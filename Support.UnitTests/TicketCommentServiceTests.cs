using Microsoft.EntityFrameworkCore;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;
using Support.Api.Services;

namespace Support.UnitTests;

public sealed class TicketCommentServiceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 12, 1, 10, 15, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsTrimmedTextAuthorAndTime()
    {
        var options = CreateOptions();
        var ticket = CreateTicket();

        await using (var setupContext = new SupportDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Tickets.Add(ticket);
            await setupContext.SaveChangesAsync();
        }

        TicketCommentResult result;
        await using (var createContext = new SupportDbContext(options))
        {
            var service = new TicketCommentService(
                new TicketRepository(createContext),
                new FakeCurrentUserContext("1"),
                new FixedClock(CreatedAt));

            result = await service.CreateAsync(ticket.Id, new CreateCommentRequest("  Пояснення учасника  "));
        }

        await using var readContext = new SupportDbContext(options);
        var savedComments = await new TicketRepository(readContext).GetCommentsByTicketIdAsync(ticket.Id);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Comment);
        Assert.Equal("Пояснення учасника", result.Comment.Text);
        Assert.Equal(1, result.Comment.AuthorId);
        Assert.Equal(CreatedAt, result.Comment.CreatedAt);
        var savedComment = Assert.Single(savedComments);
        Assert.Equal(result.Comment.Id, savedComment.Id);
        Assert.Equal(result.Comment.Text, savedComment.Text);
        Assert.Equal(result.Comment.AuthorId, savedComment.AuthorId);
        Assert.Equal(result.Comment.CreatedAt, savedComment.CreatedAt);
    }

    [Fact]
    public async Task CreateAsync_MaximumTrimmedLength_IsAccepted()
    {
        var ticket = CreateTicket();
        var repository = new FakeTicketRepository(ticket, new User { Id = 1 });
        var service = CreateService(repository, "1");

        var result = await service.CreateAsync(ticket.Id, new CreateCommentRequest($" {new string('x', 2000)} "));

        Assert.True(result.IsSuccessful);
        Assert.Equal(new string('x', 2000), result.Comment?.Text);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_OneCharacter_IsAccepted()
    {
        var ticket = CreateTicket();
        var repository = new FakeTicketRepository(ticket, new User { Id = 1 });
        var service = CreateService(repository, "1");

        var result = await service.CreateAsync(ticket.Id, new CreateCommentRequest("x"));

        Assert.True(result.IsSuccessful);
        Assert.Equal("x", result.Comment?.Text);
        Assert.Equal(1, repository.SaveCount);
    }

    [Theory]
    [MemberData(nameof(InvalidTexts))]
    public async Task CreateAsync_InvalidText_DoesNotSave(string text)
    {
        var ticket = CreateTicket();
        var repository = new FakeTicketRepository(ticket, new User { Id = 1 });
        var service = CreateService(repository, "1");

        var result = await service.CreateAsync(ticket.Id, new CreateCommentRequest(text));

        Assert.Equal(TicketCommentError.InvalidText, result.Error);
        Assert.Null(result.Comment);
        Assert.Equal(0, repository.SaveCount);
    }

    public static IEnumerable<object[]> InvalidTexts =>
    [
        [" \t\r\n "],
        [new string('x', 2001)],
        [null!]
    ];

    [Fact]
    public async Task CreateAsync_MissingTicket_ReturnsNotFoundWithoutSaving()
    {
        var repository = new FakeTicketRepository(CreateTicket(), new User { Id = 1 });
        var service = CreateService(repository, "1");

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateCommentRequest("Коментар"));

        Assert.Equal(TicketCommentError.TicketNotFound, result.Error);
        Assert.Equal(0, repository.SaveCount);
    }

    [Theory]
    [InlineData("not-a-user-id")]
    [InlineData("999")]
    public async Task CreateAsync_MissingOrInvalidAuthor_ReturnsNotFoundWithoutSaving(string userId)
    {
        var ticket = CreateTicket();
        var repository = new FakeTicketRepository(ticket, null);
        var service = CreateService(repository, userId);

        var result = await service.CreateAsync(ticket.Id, new CreateCommentRequest("Коментар"));

        Assert.Equal(TicketCommentError.AuthorNotFound, result.Error);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_ClosedTicket_ReturnsConflictWithoutSaving()
    {
        var ticket = CreateTicket(Status.Closed);
        var repository = new FakeTicketRepository(ticket, new User { Id = 1 });
        var service = CreateService(repository, "1");

        var result = await service.CreateAsync(ticket.Id, new CreateCommentRequest("Коментар"));

        Assert.Equal(TicketCommentError.TicketClosed, result.Error);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task GetCommentsByTicketIdAsync_FiltersAndOrdersComments()
    {
        var options = CreateOptions();
        var ticketId = Guid.NewGuid();
        var otherTicketId = Guid.NewGuid();
        var sameTime = CreatedAt;
        var expectedIds = new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002")
        };

        await using (var setupContext = new SupportDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.TicketComments.AddRange(
                CreateComment(expectedIds[1], ticketId, sameTime),
                CreateComment(expectedIds[0], ticketId, sameTime),
                CreateComment(Guid.NewGuid(), ticketId, sameTime.AddMinutes(1)),
                CreateComment(Guid.NewGuid(), otherTicketId, sameTime.AddMinutes(-1)));
            await setupContext.SaveChangesAsync();
        }

        await using var readContext = new SupportDbContext(options);
        var comments = await new TicketRepository(readContext).GetCommentsByTicketIdAsync(ticketId);

        Assert.Equal(expectedIds, comments.Take(2).Select(comment => comment.Id));
        Assert.Equal(3, comments.Count);
        Assert.All(comments, comment => Assert.Equal(ticketId, comment.TicketId));
        Assert.Empty(await new TicketRepository(readContext).GetCommentsByTicketIdAsync(Guid.NewGuid()));
    }

    private static DbContextOptions<SupportDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static Ticket CreateTicket(Status status = Status.New) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Заявка",
        Description = "Опис заявки",
        CategoryId = 1,
        Priority = Priority.High,
        Status = status,
        CreatedAt = CreatedAt
    };

    private static TicketComment CreateComment(Guid id, Guid ticketId, DateTimeOffset createdAt) => new()
    {
        Id = id,
        TicketId = ticketId,
        AuthorId = 1,
        Text = "Коментар",
        CreatedAt = createdAt
    };

    private static TicketCommentService CreateService(FakeTicketRepository repository, string? userId) =>
        new(repository, new FakeCurrentUserContext(userId), new FixedClock(CreatedAt));

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeCurrentUserContext(string? userId) : ICurrentUserContext
    {
        public bool IsAuthenticated => userId is not null;

        public string? UserId { get; } = userId;
    }

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

        public Task AddCommentAsync(TicketComment comment, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TicketComment>> GetCommentsByTicketIdAsync(
            Guid ticketId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TicketComment>>([]);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
