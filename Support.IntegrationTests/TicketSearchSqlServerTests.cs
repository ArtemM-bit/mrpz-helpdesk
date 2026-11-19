using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Support.Api.Data;
using Support.Api.DTOs;
using Support.Api.Models;

namespace Support.IntegrationTests;

public sealed class TicketSearchSqlServerTests
{
    [Fact]
    public async Task EmptyDatabase_HasZeroStatistics()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();

        var statistics = await new TicketRepository(database.Context).GetCategoryStatisticsAsync();

        Assert.Equal(5, statistics.Count);
        Assert.All(statistics, category => Assert.Equal(0, category.TicketCount));
        Assert.Equal(0, statistics.Sum(category => category.TicketCount));
    }

    [Fact]
    public async Task SearchAsync_MatchesUkrainianCasePairsUsingApiDatabaseCollation()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var cases = new[]
        {
            (Upper: "І", Lower: "і"),
            (Upper: "Є", Lower: "є"),
            (Upper: "Ї", Lower: "ї")
        };
        var tickets = cases.SelectMany((pair, index) => new[]
        {
            CreateTicket($"{pair.Upper}нцидент {index}", Id(index * 2 + 1)),
            CreateTicket($"{pair.Lower}сторія {index}", Id(index * 2 + 2))
        }).ToArray();
        database.Context.Tickets.AddRange(tickets);
        await database.Context.SaveChangesAsync();

        var repository = new TicketRepository(database.Context);
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
    public async Task SearchAsync_CombinesStatusPriorityAndCategoryFilters()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        var expected = CreateTicket("TDD цільова заявка", Id(1), Status.New, Priority.High, 1, assigneeId: 2);
        database.Context.Tickets.AddRange(
            expected,
            CreateTicket("TDD інший статус", Id(2), Status.Closed, Priority.High, 1, assigneeId: 2),
            CreateTicket("TDD інший пріоритет", Id(3), Status.New, Priority.Low, 1, assigneeId: 2),
            CreateTicket("TDD інша категорія", Id(4), Status.New, Priority.High, 2, assigneeId: 2),
            CreateTicket("TDD інший відповідальний", Id(5), Status.New, Priority.High, 1, assigneeId: 1));
        await database.Context.SaveChangesAsync();

        var results = await new TicketRepository(database.Context).SearchAsync(new TicketSearchRequest(
            Search: "TDD",
            CategoryId: 1,
            Priority: Priority.High,
            Status: Status.New,
            AssigneeId: 2,
            SortDirection: "asc"));

        Assert.Equal([expected.Id], results.Select(ticket => ticket.Id));
    }

    private static Ticket CreateTicket(
        string title,
        Guid id,
        Status status = Status.New,
        Priority priority = Priority.Medium,
        int categoryId = 1,
        int? assigneeId = null) => new()
    {
        Id = id,
        Title = title,
        Description = "Integration test ticket",
        CategoryId = categoryId,
        Priority = priority,
        Status = status,
        CreatedAt = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
        AssigneeId = assigneeId
    };

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private sealed class SqlServerTestDatabase : IAsyncDisposable
    {
        private const string TestDatabaseName = "SupportDbTests";
        private const string ProductionDatabaseName = "SupportDb";
        private readonly string _connectionString;

        private SqlServerTestDatabase(SupportDbContext context, string connectionString)
        {
            Context = context;
            _connectionString = connectionString;
        }

        public SupportDbContext Context { get; }

        public static async Task<SqlServerTestDatabase> CreateAsync()
        {
            var connectionString = GetTestConnectionString();
            EnsureTestDatabaseOnly(connectionString);
            await EnsureTestDatabaseCollationAsync(connectionString);

            var options = new DbContextOptionsBuilder<SupportDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            var context = new SupportDbContext(options);
            await context.Database.MigrateAsync();
            await ClearTestDataAsync(context, connectionString);

            return new SqlServerTestDatabase(context, connectionString);
        }

        public async ValueTask DisposeAsync() => await Context.DisposeAsync();

        private static string GetTestConnectionString()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var explicitTestConnection = configuration.GetConnectionString("SupportDatabaseTests");
            if (!string.IsNullOrWhiteSpace(explicitTestConnection))
            {
                return explicitTestConnection;
            }

            var apiConnection = configuration.GetConnectionString("SupportDatabase")
                ?? throw new InvalidOperationException(
                    "Set ConnectionStrings:SupportDatabaseTests or provide the Support.Api SupportDatabase connection string.");
            var builder = new SqlConnectionStringBuilder(apiConnection)
            {
                InitialCatalog = TestDatabaseName
            };
            return builder.ConnectionString;
        }

        private static void EnsureTestDatabaseOnly(string connectionString)
        {
            var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
            if (!string.Equals(databaseName, TestDatabaseName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Integration tests are restricted to '{TestDatabaseName}', not '{databaseName}'.");
            }

            if (string.Equals(databaseName, ProductionDatabaseName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Integration tests cannot run against SupportDb.");
            }
        }

        private static async Task EnsureTestDatabaseCollationAsync(string testConnectionString)
        {
            EnsureTestDatabaseOnly(testConnectionString);
            var masterBuilder = new SqlConnectionStringBuilder(testConnectionString)
            {
                InitialCatalog = "master"
            };

            await using var connection = new SqlConnection(masterBuilder.ConnectionString);
            await connection.OpenAsync();
            var supportCollation = await GetDatabaseCollationAsync(connection, ProductionDatabaseName)
                ?? throw new InvalidOperationException(
                    $"Cannot read the collation of '{ProductionDatabaseName}' to match the API database.");
            var testCollation = await GetDatabaseCollationAsync(connection, TestDatabaseName);

            if (testCollation is null)
            {
                await using var createDatabase = connection.CreateCommand();
                var quotedCollation = supportCollation.Replace("]", "]]", StringComparison.Ordinal);
                createDatabase.CommandText = $"CREATE DATABASE [SupportDbTests] COLLATE [{quotedCollation}]";
                await createDatabase.ExecuteNonQueryAsync();
                return;
            }

            if (!string.Equals(testCollation, supportCollation, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"'{TestDatabaseName}' collation '{testCollation}' must match '{ProductionDatabaseName}' collation '{supportCollation}'.");
            }
        }

        private static async Task<string?> GetDatabaseCollationAsync(SqlConnection connection, string databaseName)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(@databaseName, N'Collation'))";
            command.Parameters.AddWithValue("@databaseName", databaseName);
            var collation = await command.ExecuteScalarAsync();
            return collation is string value ? value : null;
        }

        private static async Task ClearTestDataAsync(SupportDbContext context, string connectionString)
        {
            EnsureTestDatabaseOnly(connectionString);
            context.ChangeTracker.Clear();
            context.TicketComments.RemoveRange(await context.TicketComments.ToListAsync());
            context.Tickets.RemoveRange(await context.Tickets.ToListAsync());
            await context.SaveChangesAsync();
        }
    }
}
