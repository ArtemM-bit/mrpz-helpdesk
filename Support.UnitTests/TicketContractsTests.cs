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
            CreatedAt = DateTimeOffset.UtcNow
        };

        var response = ticket.ToResponse();

        Assert.Equal(ticket.Id, response.Id);
        Assert.Equal(ticket.Title, response.Title);
        Assert.Equal(ticket.Description, response.Description);
        Assert.Equal(ticket.CategoryId, response.CategoryId);
        Assert.Equal(ticket.Priority, response.Priority);
        Assert.Equal(ticket.Status, response.Status);
        Assert.Equal(ticket.CreatedAt, response.CreatedAt);
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
    public void Enums_ContainExpectedValues()
    {
        Assert.Equal([Priority.Low, Priority.Medium, Priority.High, Priority.Critical], Enum.GetValues<Priority>());
        Assert.Equal([Status.New, Status.InProgress, Status.Resolved, Status.Closed], Enum.GetValues<Status>());
    }
}
