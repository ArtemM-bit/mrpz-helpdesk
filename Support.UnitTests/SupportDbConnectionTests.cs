using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Support.Api.Data;

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
}
