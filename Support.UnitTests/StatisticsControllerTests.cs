using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Support.Api.Controllers;
using Support.Api.Data;
using Support.Api.DTOs;

namespace Support.UnitTests;

public sealed class StatisticsControllerTests
{
    [Fact]
    public async Task GetCategories_ReturnsAllCategoriesInStableOrder()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new SupportDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var controller = new StatisticsController(new TicketRepository(context));
        var result = await controller.GetCategories(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var statistics = Assert.IsAssignableFrom<IReadOnlyList<CategoryStatisticsResponse>>(ok.Value);
        Assert.Equal("api/statistics", typeof(StatisticsController).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal("categories", typeof(StatisticsController).GetMethod(nameof(controller.GetCategories))
            ?.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal([1, 2, 3, 4, 5], statistics.Select(category => category.CategoryId));
        Assert.All(statistics, category => Assert.Equal(0, category.TicketCount));
        Assert.All(statistics, category => Assert.False(string.IsNullOrWhiteSpace(category.CategoryName)));
    }
}
