using Microsoft.EntityFrameworkCore;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;
using Support.Api.Services;

namespace Support.UnitTests;

public sealed class CreateTicketRequestValidatorTests
{
    [Fact]
    public async Task ValidateAsync_AcceptsMaximumLengths()
    {
        await using var context = await CreateContextWithCategoryAsync();
        var validator = new CreateTicketRequestValidator(context);
        var request = ValidRequest() with
        {
            Title = $" {new string('t', 150)} ",
            Description = $" {new string('d', 4000)} "
        };

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.Equal(new string('t', 150), result.NormalizedRequest!.Title);
        Assert.Equal(new string('d', 4000), result.NormalizedRequest.Description);
    }

    [Fact]
    public async Task ValidateAsync_AcceptsOneCharacterAfterTrimming()
    {
        await using var context = await CreateContextWithCategoryAsync();
        var validator = new CreateTicketRequestValidator(context);
        var request = ValidRequest() with { Title = " x ", Description = " y " };

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.Equal("x", result.NormalizedRequest!.Title);
        Assert.Equal("y", result.NormalizedRequest.Description);
    }

    [Fact]
    public async Task ValidateAsync_TrimsTitleAndDescription()
    {
        await using var context = await CreateContextWithCategoryAsync();
        var validator = new CreateTicketRequestValidator(context);
        var request = ValidRequest() with { Title = "  title  ", Description = "  description  " };

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.Equal("title", result.NormalizedRequest!.Title);
        Assert.Equal("description", result.NormalizedRequest.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_RejectsMissingTitle(string? title)
    {
        await using var context = await CreateContextWithCategoryAsync();
        var validator = new CreateTicketRequestValidator(context);
        var request = ValidRequest() with { Title = title! };

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(CreateTicketRequest.Title), result.Errors.Keys);
    }

    [Fact]
    public async Task ValidateAsync_RejectsTitleLongerThan150Characters()
    {
        await using var context = await CreateContextWithCategoryAsync();
        var validator = new CreateTicketRequestValidator(context);
        var request = ValidRequest() with { Title = new string('t', 151) };

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(CreateTicketRequest.Title), result.Errors.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_RejectsMissingDescription(string? description)
    {
        await using var context = await CreateContextWithCategoryAsync();
        var validator = new CreateTicketRequestValidator(context);
        var request = ValidRequest() with { Description = description! };

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(CreateTicketRequest.Description), result.Errors.Keys);
    }

    [Fact]
    public async Task ValidateAsync_RejectsDescriptionLongerThan4000Characters()
    {
        await using var context = await CreateContextWithCategoryAsync();
        var validator = new CreateTicketRequestValidator(context);
        var request = ValidRequest() with { Description = new string('d', 4001) };

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(CreateTicketRequest.Description), result.Errors.Keys);
    }

    [Fact]
    public async Task ValidateAsync_RejectsUnknownCategoryAndPriority()
    {
        await using var context = await CreateContextWithCategoryAsync();
        var validator = new CreateTicketRequestValidator(context);
        var request = ValidRequest() with { CategoryId = 999, Priority = (Priority)999 };

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(CreateTicketRequest.CategoryId), result.Errors.Keys);
        Assert.Contains(nameof(CreateTicketRequest.Priority), result.Errors.Keys);
        Assert.All(result.Errors.Values.SelectMany(messages => messages), message =>
            Assert.False(string.IsNullOrWhiteSpace(message)));
    }

    [Fact]
    public async Task ValidateAsync_DoesNotAddTicketWhenRequestIsInvalid()
    {
        await using var context = await CreateContextWithCategoryAsync();
        var validator = new CreateTicketRequestValidator(context);
        var request = ValidRequest() with { Title = " " };

        await validator.ValidateAsync(request);

        Assert.Empty(await context.Tickets.ToListAsync());
    }

    private static CreateTicketRequest ValidRequest() => new()
    {
        Title = "Title",
        Description = "Description",
        CategoryId = 1,
        Priority = Priority.Medium
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
}
