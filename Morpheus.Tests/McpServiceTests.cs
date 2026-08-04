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

    [Fact]
    public async Task GetLeaderboardAsync_IsGuildScopedAndValidatesBounds()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);
        Guild otherGuild = new() { DiscordId = 999, Name = "Other" };
        testDb.Db.Guilds.Add(otherGuild);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.UserActivity.AddRange(
            CreateActivity(guild.Id, user.Id, 10),
            CreateActivity(otherGuild.Id, user.Id, 1000));
        await testDb.Db.SaveChangesAsync();

        McpService service = new(testDb.Db);
        IReadOnlyList<McpLeaderboardEntry> result = await service.GetLeaderboardAsync(
            "xp", guild.Id, 30, 10);

        McpLeaderboardEntry entry = Assert.Single(result);
        Assert.Equal(10, entry.Value);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetLeaderboardAsync("xp", guild.Id, 366, 10));
    }

    private static UserActivity CreateActivity(int guildId, int userId, int xp) => new()
    {
        GuildId = guildId,
        UserId = userId,
        DiscordChannelId = 1,
        DiscordMessageId = (ulong)Random.Shared.Next(1, int.MaxValue),
        XpGained = xp,
        MessageLength = 25,
        InsertDate = DateTime.UtcNow.AddHours(-1)
    };

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
