using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Support.Api.Data;
using Support.Api.Models;

namespace Support.UnitTests;

public sealed class SupportDbConnectionTests
{
    [Fact]
    public async Task SupportDatabase_connection_executes_select_one()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("SupportDatabase");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var dbContext = new SupportDbContext(options);
        await dbContext.Database.OpenConnectionAsync();

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT 1";
        var result = await command.ExecuteScalarAsync();

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task SupportDatabase_migrationsAreRepeatableAndRejectUnknownCategory()
    {
        var connectionString = GetSupportDatabaseConnectionString();
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var dbContext = new SupportDbContext(options);
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.MigrateAsync();

        var categories = await dbContext.Categories.ToListAsync();
        Assert.Equal(5, categories.Count);
        Assert.Equal(5, categories.Select(category => category.Name).Distinct().Count());

        dbContext.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Заявка без категории",
            Description = "Проверка внешнего ключа",
            CategoryId = int.MaxValue,
            Priority = Priority.Medium,
            Status = Status.New,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    private static string GetSupportDatabaseConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("SupportDatabase")!;
    }
}
