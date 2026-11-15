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

    [Fact]
    public async Task SupportDatabase_AllowsUnassignedTicketsAndRejectsCommentsWithUnknownReferences()
    {
        var connectionString = GetSupportDatabaseConnectionString();
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var dbContext = new SupportDbContext(options);
        await dbContext.Database.MigrateAsync();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Заявка без відповідального",
            Description = "Перевірка nullable-полів",
            CategoryId = 1,
            Priority = Priority.Low,
            Status = Status.New,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await dbContext.Tickets.AddAsync(ticket);
        await dbContext.SaveChangesAsync();
        Assert.Null(ticket.AssigneeId);
        Assert.Null(ticket.ResolvedAt);

        try
        {
            await dbContext.TicketComments.AddAsync(new TicketComment
            {
                Id = Guid.NewGuid(),
                TicketId = Guid.NewGuid(),
                AuthorId = 1,
                Text = "Посилання на невідому заявку",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
            dbContext.ChangeTracker.Clear();

            await dbContext.TicketComments.AddAsync(new TicketComment
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                AuthorId = int.MaxValue,
                Text = "Посилання на невідомого автора",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
            dbContext.ChangeTracker.Clear();
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
            dbContext.Tickets.Remove(ticket);
            await dbContext.SaveChangesAsync();
        }
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
