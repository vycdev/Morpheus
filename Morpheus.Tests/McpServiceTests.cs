using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.MCP;

namespace Morpheus.Tests;

public class McpServiceTests
{
    [Fact]
    public async Task GetApprovedQuotesAsync_NeverReturnsPendingOrRemovedQuotes()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);

        testDb.Db.Quotes.AddRange(
            new Quote
            {
                GuildId = guild.Id,
                UserId = user.Id,
                Content = "approved",
                Approved = true
            },
            new Quote
            {
                GuildId = guild.Id,
                UserId = user.Id,
                Content = "pending",
                Approved = false
            },
            new Quote
            {
                GuildId = guild.Id,
                UserId = user.Id,
                Content = "removed",
                Approved = true,
                Removed = true
            });
        await testDb.Db.SaveChangesAsync();

        McpQuotePage result = await new McpService(testDb.Db).GetApprovedQuotesAsync();

        McpQuoteItem item = Assert.Single(result.Items);
        Assert.Equal("approved", item.Content);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task GetApprovedQuoteAsync_ReturnsNullForPendingOrRemovedQuote()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);

        Quote pending = new()
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = "pending",
            Approved = false
        };
        Quote removed = new()
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = "removed",
            Approved = true,
            Removed = true
        };
        testDb.Db.Quotes.AddRange(pending, removed);
        await testDb.Db.SaveChangesAsync();

        McpService service = new(testDb.Db);

        Assert.Null(await service.GetApprovedQuoteAsync(pending.Id));
        Assert.Null(await service.GetApprovedQuoteAsync(removed.Id));
    }

    [Fact]
    public async Task ApprovedQuoteScores_SupportTotalsLargerThanIntMaxValue()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);
        User secondUser = new() { DiscordId = 789, Username = "SecondUser" };
        testDb.Db.Users.Add(secondUser);
        await testDb.Db.SaveChangesAsync();

        Quote quote = new()
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = "high score",
            Approved = true
        };
        testDb.Db.Quotes.Add(quote);
        await testDb.Db.SaveChangesAsync();
        testDb.Db.QuoteScores.AddRange(
            new QuoteScore { QuoteId = quote.Id, UserId = user.Id, Score = int.MaxValue },
            new QuoteScore { QuoteId = quote.Id, UserId = secondUser.Id, Score = 1 });
        await testDb.Db.SaveChangesAsync();

        McpService service = new(testDb.Db);
        McpQuotePage page = await service.GetApprovedQuotesAsync(sort: "score");
        McpQuoteDetail? detail = await service.GetApprovedQuoteAsync(quote.Id);

        Assert.Equal((long)int.MaxValue + 1, Assert.Single(page.Items).Score);
        Assert.Equal((long)int.MaxValue + 1, detail?.TotalScore);
    }

    [Fact]
    public async Task GetGuildInfoAsync_ReturnsOnlyAggregateGuildData()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);
        testDb.Db.UserLevels.Add(new UserLevels
        {
            GuildId = guild.Id,
            UserId = user.Id,
            TotalXp = 500,
            UserMessageCount = 10,
            Level = 3
        });
        await testDb.Db.SaveChangesAsync();

        McpGuildInfo? result = await new McpService(testDb.Db)
            .GetGuildInfoAsync(guild.Id, null);

        Assert.NotNull(result);
        Assert.Equal(guild.Id, result.Id);
        Assert.Equal("Test Server", result.Name);
        Assert.Equal(1, result.TrackedUsers);
        Assert.Equal(10, result.Messages);
        Assert.Equal(500, result.Xp);
    }

    private sealed class SqliteTestDb(SqliteConnection connection, DB db) : IAsyncDisposable
    {
        public DB Db { get; } = db;

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private static async Task<SqliteTestDb> CreateSqliteDbAsync()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();
        DB db = new(new DbContextOptionsBuilder<DB>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return new SqliteTestDb(connection, db);
    }

    private static async Task<(Guild Guild, User User)> SeedBaseAsync(DB db)
    {
        Guild guild = new()
        {
            DiscordId = 123,
            Name = "Test Server",
            Prefix = "m!"
        };
        User user = new()
        {
            DiscordId = 456,
            Username = "TestUser",
            Balance = 1000m
        };
        db.Guilds.Add(guild);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (guild, user);
    }
}
