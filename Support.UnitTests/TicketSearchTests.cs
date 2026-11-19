using System.ComponentModel.DataAnnotations;
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

        var uppercaseSearch = await repository.SearchAsync(new TicketSearchRequest(Search: "ПРИНТЕР"));
        var lowercaseSearch = await repository.SearchAsync(new TicketSearchRequest(Search: " принтер "));
        var whitespaceSearch = await repository.SearchAsync(new TicketSearchRequest(Search: "   "));
        var noMatches = await repository.SearchAsync(new TicketSearchRequest(Search: "невідомий текст"));

        Assert.Equal([titleMatch.Id, descriptionMatch.Id], uppercaseSearch.Select(ticket => ticket.Id));
        Assert.Equal([titleMatch.Id, descriptionMatch.Id], lowercaseSearch.Select(ticket => ticket.Id));
        Assert.Equal(3, whitespaceSearch.Count);
        Assert.Empty(noMatches);
    }

    [Fact]
    public async Task SearchAsync_MatchesUkrainianCasePairs()
    {
        await using var context = await CreateContextAsync();
        var cases = new[]
        {
            (Upper: "ІКС", Lower: "ікс"),
            (Upper: "ЄКС", Lower: "єкс"),
            (Upper: "ЇКС", Lower: "їкс"),
            (Upper: "ҐКС", Lower: "ґкс")
        };
        var tickets = cases.SelectMany((pair, index) => new[]
        {
            CreateTicket($"{pair.Upper} token {index}", "", id: Id(index * 2 + 1)),
            CreateTicket($"{pair.Lower} token {index}", "", id: Id(index * 2 + 2))
        }).ToArray();
        context.Tickets.AddRange(tickets);
        await context.SaveChangesAsync();

        var repository = new TicketRepository(context);
        for (var index = 0; index < cases.Length; index++)
        {
            var pair = cases[index];
            var expectedIds = new[] { tickets[index * 2].Id, tickets[index * 2 + 1].Id };
            var lowerSearch = await repository.SearchAsync(new TicketSearchRequest(Search: pair.Lower));
            var upperSearch = await repository.SearchAsync(new TicketSearchRequest(Search: pair.Upper));

            Assert.Equal(expectedIds, lowerSearch.Select(ticket => ticket.Id).Order());
            Assert.Equal(expectedIds, upperSearch.Select(ticket => ticket.Id).Order());
        }
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
    public async Task SearchAsync_FiltersByAssigneeId()
    {
        await using var context = await CreateContextAsync();
        var assignedToOne = CreateTicket("One", "Description", id: Id(1));
        assignedToOne.AssigneeId = 1;
        var assignedToTwo = CreateTicket("Two", "Description", id: Id(2));
        assignedToTwo.AssigneeId = 2;
        var unassigned = CreateTicket("Unassigned", "Description", id: Id(3));
        context.Tickets.AddRange(assignedToOne, assignedToTwo, unassigned);
        await context.SaveChangesAsync();

        var results = await new TicketRepository(context).SearchAsync(new TicketSearchRequest(AssigneeId: 2));

        Assert.Equal([assignedToTwo.Id], results.Select(ticket => ticket.Id));
    }

    [Fact]
    public async Task SearchAsync_UsesUkrainianCaseInsensitiveTextWithAllFilters()
    {
        await using var context = await CreateContextAsync();
        var matching = CreateTicket("ҐАНОК цільова заявка", "І Є Ї Ґ", Status.New, Priority.High, 1, Id(1));
        matching.AssigneeId = 2;
        var otherStatus = CreateTicket("ґанок інший статус", "", Status.Closed, Priority.High, 1, Id(2));
        otherStatus.AssigneeId = 2;
        var otherPriority = CreateTicket("ґанок інший пріоритет", "", Status.New, Priority.Low, 1, Id(3));
        otherPriority.AssigneeId = 2;
        var otherCategory = CreateTicket("ґанок інша категорія", "", Status.New, Priority.High, 2, Id(4));
        otherCategory.AssigneeId = 2;
        var otherAssignee = CreateTicket("ґанок інший відповідальний", "", Status.New, Priority.High, 1, Id(5));
        otherAssignee.AssigneeId = 1;
        var otherText = CreateTicket("Інша заявка", "", Status.New, Priority.High, 1, Id(6));
        otherText.AssigneeId = 2;
        context.Tickets.AddRange(matching, otherStatus, otherPriority, otherCategory, otherAssignee, otherText);
        await context.SaveChangesAsync();

        var repository = new TicketRepository(context);
        var lowerCaseSearch = await repository.SearchAsync(new TicketSearchRequest(
            Search: "ґанок", CategoryId: 1, Priority: Priority.High, Status: Status.New, AssigneeId: 2));
        var upperCaseSearch = await repository.SearchAsync(new TicketSearchRequest(
            Search: "ҐАНОК", CategoryId: 1, Priority: Priority.High, Status: Status.New, AssigneeId: 2));

        Assert.Equal([matching.Id], lowerCaseSearch.Select(ticket => ticket.Id));
        Assert.Equal([matching.Id], upperCaseSearch.Select(ticket => ticket.Id));
    }

    [Fact]
    public async Task SearchAsync_LikeSpecialCharactersAreLiteralAndUnknownIdsReturnNoResults()
    {
        await using var context = await CreateContextAsync();
        var literalPercent = CreateTicket("Upload 100% done", "", id: Id(1));
        var wildcardPercent = CreateTicket("Upload 100x done", "", id: Id(2));
        var literalUnderscore = CreateTicket("test_1", "", id: Id(3));
        var wildcardUnderscore = CreateTicket("testA1", "", id: Id(4));
        var literalBracket = CreateTicket("test[1]", "", id: Id(5));
        var wildcardBracket = CreateTicket("test1", "", id: Id(6));
        var assignedTicket = CreateTicket("Assigned", "", id: Id(7));
        assignedTicket.AssigneeId = 1;
        context.Tickets.AddRange(
            literalPercent, wildcardPercent, literalUnderscore, wildcardUnderscore,
            literalBracket, wildcardBracket, assignedTicket);
        await context.SaveChangesAsync();

        var repository = new TicketRepository(context);
        var percentResults = await repository.SearchAsync(new TicketSearchRequest(Search: "100%"));
        var underscoreResults = await repository.SearchAsync(new TicketSearchRequest(Search: "test_1"));
        var bracketResults = await repository.SearchAsync(new TicketSearchRequest(Search: "test[1]"));
        var unknownCategory = await repository.SearchAsync(new TicketSearchRequest(CategoryId: 9999));
        var unknownAssignee = await repository.SearchAsync(new TicketSearchRequest(AssigneeId: 9999));

        Assert.Equal([literalPercent.Id], percentResults.Select(ticket => ticket.Id));
        Assert.Equal([literalUnderscore.Id], underscoreResults.Select(ticket => ticket.Id));
        Assert.Equal([literalBracket.Id], bracketResults.Select(ticket => ticket.Id));
        Assert.Empty(unknownCategory);
        Assert.Empty(unknownAssignee);
    }

    [Fact]
    public void TicketSearchRequest_ValidatesPositiveIdsDefinedEnumsAndSortDirection()
    {
        var invalidRequests = new[]
        {
            new TicketSearchRequest(CategoryId: 0),
            new TicketSearchRequest(AssigneeId: -1),
            new TicketSearchRequest(Priority: (Priority)99),
            new TicketSearchRequest(Status: (Status)99),
            new TicketSearchRequest(SortDirection: "sideways")
        };

        foreach (var request in invalidRequests)
        {
            var validationResults = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                validationResults,
                validateAllProperties: true);

            Assert.False(isValid);
            Assert.NotEmpty(validationResults);
            Assert.All(validationResults, result => Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage)));
        }

        Assert.Equal("desc", new TicketSearchRequest().SortDirection);
        Assert.True(Validate(new TicketSearchRequest(SortDirection: " ASC ")));
        Assert.True(Validate(new TicketSearchRequest(CategoryId: 1, AssigneeId: 2)));
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

    private static bool Validate(TicketSearchRequest request)
    {
        var validationResults = new List<ValidationResult>();
        return Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);
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
