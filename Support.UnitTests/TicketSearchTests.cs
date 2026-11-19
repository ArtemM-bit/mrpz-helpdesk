using Microsoft.EntityFrameworkCore;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;

namespace Support.UnitTests;

public sealed class TicketSearchTests
{
    [Fact]
    public async Task SearchAsync_SearchesTitleAndDescriptionIgnoringCaseAndWhitespace()
    {
        await using var context = await CreateContextAsync();
        var titleMatch = CreateTicket("ПРИНТЕР не працює", "Перевірка кабелю", id: Id(1));
        var descriptionMatch = CreateTicket("Мережа недоступна", "Збій принтера після оновлення", id: Id(2));
        var unrelated = CreateTicket("Не вмикається монітор", "Потрібна діагностика", id: Id(3));
        context.Tickets.AddRange(titleMatch, descriptionMatch, unrelated);
        await context.SaveChangesAsync();

        var repository = new TicketRepository(context);

        var uppercaseSearch = await repository.SearchAsync(new TicketSearchRequest(SearchText: "ПРИНТЕР"));
        var lowercaseSearch = await repository.SearchAsync(new TicketSearchRequest(SearchText: " принтер "));
        var whitespaceSearch = await repository.SearchAsync(new TicketSearchRequest(SearchText: "   "));
        var noMatches = await repository.SearchAsync(new TicketSearchRequest(SearchText: "невідомий текст"));

        Assert.Equal([titleMatch.Id, descriptionMatch.Id], uppercaseSearch.Select(ticket => ticket.Id));
        Assert.Equal([titleMatch.Id, descriptionMatch.Id], lowercaseSearch.Select(ticket => ticket.Id));
        Assert.Equal(3, whitespaceSearch.Count);
        Assert.Empty(noMatches);
    }

    [Fact]
    public async Task SearchAsync_AppliesEachFilterIndividuallyAndCombinesAllThree()
    {
        await using var context = await CreateContextAsync();
        var matching = CreateTicket("Цільова заявка", "TDD", Status.New, Priority.High, 1, Id(1));
        var otherStatus = CreateTicket("Заявка", "TDD", Status.Closed, Priority.High, 1, Id(2));
        var otherPriority = CreateTicket("Заявка", "TDD", Status.New, Priority.Low, 1, Id(3));
        var otherCategory = CreateTicket("Заявка", "TDD", Status.New, Priority.High, 2, Id(4));
        var otherTicket = CreateTicket("Заявка", "TDD", Status.InProgress, Priority.Medium, 3, Id(5));
        context.Tickets.AddRange(matching, otherStatus, otherPriority, otherCategory, otherTicket);
        await context.SaveChangesAsync();

        var repository = new TicketRepository(context);
        var byStatus = await repository.SearchAsync(new TicketSearchRequest(Status: Status.New));
        var byPriority = await repository.SearchAsync(new TicketSearchRequest(Priority: Priority.High));
        var byCategory = await repository.SearchAsync(new TicketSearchRequest(CategoryId: 1));
        var combined = await repository.SearchAsync(new TicketSearchRequest(
            Status: Status.New,
            Priority: Priority.High,
            CategoryId: 1));

        Assert.Equal([matching.Id, otherPriority.Id, otherCategory.Id], byStatus.Select(ticket => ticket.Id));
        Assert.Equal([matching.Id, otherStatus.Id, otherCategory.Id], byPriority.Select(ticket => ticket.Id));
        Assert.Equal([matching.Id, otherStatus.Id, otherPriority.Id], byCategory.Select(ticket => ticket.Id));
        Assert.Equal([matching.Id], combined.Select(ticket => ticket.Id));
    }

    [Fact]
    public async Task SearchAsync_SortsBothDirectionsAndBreaksDateTiesById()
    {
        await using var context = await CreateContextAsync();
        var createdAt = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var lowId = CreateTicket("Low", "Description", createdAt: createdAt, id: Id(1));
        var highId = CreateTicket("High", "Description", createdAt: createdAt, id: Id(2));
        var newer = CreateTicket("Newer", "Description", createdAt: createdAt.AddHours(1), id: Id(3));
        context.Tickets.AddRange(highId, newer, lowId);
        await context.SaveChangesAsync();

        var repository = new TicketRepository(context);
        var ascending = await repository.SearchAsync(new TicketSearchRequest(SortDirection: "asc"));
        var descending = await repository.SearchAsync(new TicketSearchRequest(SortDirection: "desc"));

        Assert.Equal([lowId.Id, highId.Id, newer.Id], ascending.Select(ticket => ticket.Id));
        Assert.Equal([newer.Id, lowId.Id, highId.Id], descending.Select(ticket => ticket.Id));
    }

    [Fact]
    public async Task GetCategoryStatisticsAsync_ReturnsCountsForPopulatedAndEmptyCategories()
    {
        await using var context = await CreateContextAsync();
        context.Tickets.AddRange(
            CreateTicket("First", "Description", categoryId: 1),
            CreateTicket("Second", "Description", categoryId: 1),
            CreateTicket("Third", "Description", categoryId: 2));
        await context.SaveChangesAsync();

        var statistics = await new TicketRepository(context).GetCategoryStatisticsAsync();

        Assert.Equal(2, statistics.Single(category => category.CategoryId == 1).TicketCount);
        Assert.Equal(1, statistics.Single(category => category.CategoryId == 2).TicketCount);
        Assert.Equal(0, statistics.Single(category => category.CategoryId == 3).TicketCount);
        Assert.Equal(3, statistics.Sum(category => category.TicketCount));
    }

    private static async Task<SupportDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new SupportDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static Ticket CreateTicket(
        string title,
        string description,
        Status status = Status.New,
        Priority priority = Priority.Medium,
        int categoryId = 1,
        Guid? id = null,
        DateTimeOffset? createdAt = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Title = title,
        Description = description,
        CategoryId = categoryId,
        Priority = priority,
        Status = status,
        CreatedAt = createdAt ?? DateTimeOffset.UnixEpoch
    };

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
