using Microsoft.EntityFrameworkCore;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;

namespace Support.UnitTests;

public sealed class TicketContractsTests
{
    [Fact]
    public void CreateTicketRequest_DoesNotExposeSystemFields()
    {
        var propertyNames = typeof(CreateTicketRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(
            ["Title", "Description", "CategoryId", "Priority"],
            propertyNames);
    }

    [Fact]
    public void CreateTicketRequest_ToTicket_AssignsSystemFields()
    {
        var createdAt = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var request = new CreateTicketRequest
        {
            Title = "Не работает принтер",
            Description = "Принтер не печатает документы.",
            CategoryId = 3,
            Priority = Priority.High
        };

        var ticket = request.ToTicket(createdAt);

        Assert.NotEqual(Guid.Empty, ticket.Id);
        Assert.Equal(Status.New, ticket.Status);
        Assert.Equal(createdAt, ticket.CreatedAt);
        Assert.Null(ticket.AssigneeId);
        Assert.Null(ticket.ResolvedAt);
        Assert.Null(ticket.DueAt);
        Assert.Equal(TimeSpan.Zero, ticket.CreatedAt.Offset);
        Assert.Equal(request.Title, ticket.Title);
        Assert.Equal(request.Description, ticket.Description);
        Assert.Equal(request.CategoryId, ticket.CategoryId);
        Assert.Equal(request.Priority, ticket.Priority);
    }

    [Fact]
    public void Ticket_ToResponse_MapsAllTicketFields()
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Заявка",
            Description = "Описание",
            CategoryId = 2,
            Priority = Priority.Critical,
            Status = Status.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            AssigneeId = 2,
            ResolvedAt = DateTimeOffset.UtcNow,
            DueAt = new DateTimeOffset(2026, 1, 20, 15, 0, 0, TimeSpan.FromHours(2))
        };

        var now = new DateTimeOffset(2026, 1, 21, 12, 0, 0, TimeSpan.Zero);
        var response = ticket.ToResponse(now);

        Assert.Equal(ticket.Id, response.Id);
        Assert.Equal(ticket.Title, response.Title);
        Assert.Equal(ticket.Description, response.Description);
        Assert.Equal(ticket.CategoryId, response.CategoryId);
        Assert.Equal(ticket.Priority, response.Priority);
        Assert.Equal(ticket.Status, response.Status);
        Assert.Equal(ticket.CreatedAt, response.CreatedAt);
        Assert.Equal(ticket.AssigneeId, response.AssigneeId);
        Assert.Equal(ticket.ResolvedAt?.ToUniversalTime(), response.ResolvedAt);
        Assert.Equal(ticket.DueAt?.ToUniversalTime(), response.DueAt);
        Assert.True(response.IsOverdue);
    }

    [Fact]
    public void Ticket_ToResponse_MapsNullDueAtWithoutOverdue()
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Заявка",
            Description = "Описание",
            CategoryId = 1,
            Priority = Priority.Low,
            Status = Status.New,
            CreatedAt = DateTimeOffset.UtcNow,
            DueAt = null
        };

        var response = ticket.ToResponse();

        Assert.Null(response.DueAt);
        Assert.False(response.IsOverdue);
    }

    [Fact]
    public void SupportDbContext_ConfiguresCategoryToTicketRelationship()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=(local);Database=SupportDb;Trusted_Connection=True;")
            .Options;

        using var context = new SupportDbContext(options);
        var relationship = context.Model.FindEntityType(typeof(Ticket))!
            .GetForeignKeys()
            .Single(foreignKey => foreignKey.Properties.Single().Name == nameof(Ticket.CategoryId));

        Assert.Equal(typeof(Category), relationship.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
    }

    [Fact]
    public void SupportDbContext_ConfiguresOptionalAssigneeAndRequiredCommentRelationships()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=(local);Database=SupportDb;Trusted_Connection=True;")
            .Options;

        using var context = new SupportDbContext(options);
        var ticketType = context.Model.FindEntityType(typeof(Ticket))!;
        var assigneeRelationship = ticketType.GetForeignKeys()
            .Single(foreignKey => foreignKey.Properties.Single().Name == nameof(Ticket.AssigneeId));
        var commentType = context.Model.FindEntityType(typeof(TicketComment))!;
        var ticketRelationship = commentType.GetForeignKeys()
            .Single(foreignKey => foreignKey.Properties.Single().Name == nameof(TicketComment.TicketId));
        var authorRelationship = commentType.GetForeignKeys()
            .Single(foreignKey => foreignKey.Properties.Single().Name == nameof(TicketComment.AuthorId));

        Assert.False(assigneeRelationship.IsRequired);
        Assert.Equal(DeleteBehavior.SetNull, assigneeRelationship.DeleteBehavior);
        Assert.Equal(typeof(Ticket), ticketRelationship.PrincipalEntityType.ClrType);
        Assert.True(ticketRelationship.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, ticketRelationship.DeleteBehavior);
        Assert.Equal(typeof(User), authorRelationship.PrincipalEntityType.ClrType);
        Assert.True(authorRelationship.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, authorRelationship.DeleteBehavior);
    }

    [Fact]
    public void SupportDbContext_UsesUnicodeColumnsForText()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=(local);Database=SupportDb;Trusted_Connection=True;")
            .Options;

        using var context = new SupportDbContext(options);
        var categoryName = context.Model.FindEntityType(typeof(Category))!
            .FindProperty(nameof(Category.Name))!;
        var ticketTitle = context.Model.FindEntityType(typeof(Ticket))!
            .FindProperty(nameof(Ticket.Title))!;
        var ticketDescription = context.Model.FindEntityType(typeof(Ticket))!
            .FindProperty(nameof(Ticket.Description))!;

        Assert.Equal("nvarchar(100)", categoryName.GetColumnType());
        Assert.Equal("nvarchar(200)", ticketTitle.GetColumnType());
        Assert.Equal("nvarchar(4000)", ticketDescription.GetColumnType());
    }

    [Fact]
    public async Task SupportDbContext_SeedsExpectedCategories()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new SupportDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var names = await context.Categories
            .OrderBy(category => category.Id)
            .Select(category => category.Name)
            .ToArrayAsync();

        Assert.Equal(
            ["обладнання", "програмне забезпечення", "мережа", "доступ до облікового запису", "інше"],
            names);
    }

    [Fact]
    public async Task SupportDbContext_SeedsDemoUsers()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new SupportDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var users = await context.Users
            .OrderBy(user => user.Id)
            .ToArrayAsync();

        Assert.Equal(2, users.Length);
        Assert.False(users[0].IsSupportEmployee);
        Assert.True(users[1].IsSupportEmployee);
        Assert.All(users, user => Assert.False(string.IsNullOrWhiteSpace(user.FullName)));
    }

    [Fact]
    public void SupportDbContext_ModelsDueAtAsNullableAndDoesNotPersistIsOverdue()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer("Server=(local);Database=SupportDb;Trusted_Connection=True;")
            .Options;

        using var context = new SupportDbContext(options);
        var ticketType = context.Model.FindEntityType(typeof(Ticket))!;
        var dueAt = ticketType.FindProperty(nameof(Ticket.DueAt))!;

        Assert.True(dueAt.IsNullable);
        Assert.Equal("datetimeoffset", dueAt.GetColumnType());
        Assert.Null(ticketType.FindProperty(nameof(TicketResponse.IsOverdue)));
    }

    [Fact]
    public void Enums_ContainExpectedValues()
    {
        Assert.Equal([Priority.Low, Priority.Medium, Priority.High, Priority.Critical], Enum.GetValues<Priority>());
        Assert.Equal([Status.New, Status.InProgress, Status.Resolved, Status.Closed], Enum.GetValues<Status>());
    }
}
