using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.MCP;

namespace Morpheus.Tests;

public class McpServiceTests
{
    [Fact]
    public async Task GetUserStatsAsync_ReturnsUserStats_WhenUserExists()
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
        testDb.Db.UserActivity.Add(new UserActivity
        {
            GuildId = guild.Id,
            UserId = user.Id,
            DiscordChannelId = 1,
            DiscordMessageId = 2,
            XpGained = 50,
            MessageLength = 25,
            InsertDate = DateTime.UtcNow.AddDays(-1)
        });
        await testDb.Db.SaveChangesAsync();

        McpService service = CreateService(testDb.Db);

        McpUserStats? stats = await service.GetUserStatsAsync(user.Id, null);

        Assert.NotNull(stats);
        Assert.Equal(user.Id, stats.Id);
        Assert.Equal(user.DiscordId, stats.DiscordId);
        Assert.Equal(user.Username, stats.Username);
        Assert.Equal(1000m, stats.Balance);
        Assert.Equal(10, stats.TotalMessages);
        Assert.Equal(500, stats.TotalXp);
        Assert.Equal(3, stats.Level);
    }

    [Fact]
    public async Task GetUserStatsAsync_ReturnsNull_WhenUserNotFound()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        McpService service = CreateService(testDb.Db);

        McpUserStats? stats = await service.GetUserStatsAsync(999, null);

        Assert.Null(stats);
    }

    [Fact]
    public async Task GetGuildInfoAsync_ReturnsGuildInfo_WhenGuildExists()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);

        testDb.Db.UserLevels.Add(new UserLevels
        {
            GuildId = guild.Id,
            UserId = user.Id,
            TotalXp = 200,
            UserMessageCount = 5,
            Level = 2
        });
        testDb.Db.Quotes.Add(new Quote
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = "test quote",
            Approved = true
        });
        await testDb.Db.SaveChangesAsync();

        McpService service = CreateService(testDb.Db);

        McpGuildInfo? info = await service.GetGuildInfoAsync(guild.Id, null);

        Assert.NotNull(info);
        Assert.Equal(guild.Id, info.Id);
        Assert.Equal(guild.DiscordId, info.DiscordId);
        Assert.Equal(guild.Name, info.Name);
        Assert.Equal(guild.Prefix, info.Prefix);
        Assert.Equal(1, info.TrackedUsers);
        Assert.Equal(5, info.Messages);
        Assert.Equal(200, info.Xp);
        Assert.Equal(1, info.ApprovedQuotes);
    }

    [Fact]
    public async Task GetEconomySummaryAsync_ReturnsSummary()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        await SeedBaseAsync(testDb.Db);

        // Add a second user with balance
        testDb.Db.Users.Add(new User
        {
            DiscordId = 999,
            Username = "user2",
            Balance = 500m
        });
        await testDb.Db.SaveChangesAsync();

        McpService service = CreateService(testDb.Db);

        McpEconomySummary summary = await service.GetEconomySummaryAsync();

        Assert.Equal(2, summary.TotalUsers);
        Assert.Equal(1500m, summary.TotalBalance);
        Assert.Equal(750m, summary.AverageBalance);
        Assert.Equal(0m, summary.UbiPoolSize);
    }

    [Fact]
    public async Task GetActivityOverviewAsync_ReturnsOverview()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);

        testDb.Db.UserLevels.Add(new UserLevels
        {
            GuildId = guild.Id,
            UserId = user.Id,
            TotalXp = 300,
            UserMessageCount = 7,
            Level = 2
        });
        testDb.Db.UserActivity.Add(new UserActivity
        {
            GuildId = guild.Id,
            UserId = user.Id,
            DiscordChannelId = 1,
            DiscordMessageId = 2,
            XpGained = 100,
            MessageLength = 50,
            InsertDate = DateTime.UtcNow.AddHours(-1)
        });
        await testDb.Db.SaveChangesAsync();

        McpService service = CreateService(testDb.Db);

        McpActivityOverview overview = await service.GetActivityOverviewAsync();

        Assert.Equal(7, overview.TotalMessages);
        Assert.Equal(300, overview.TotalXp);
        Assert.Equal(1, overview.ActiveUsersLast30Days);
        Assert.Equal(1, overview.MessagesLast30Days);
        Assert.Equal(100, overview.XpLast30Days);
        Assert.Equal(1, overview.TotalServers);
        Assert.Equal(1, overview.TotalKnownUsers);
    }

    [Fact]
    public async Task GetGuildsAsync_ReturnsGuildList()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        await SeedBaseAsync(testDb.Db);

        // Add a second guild
        testDb.Db.Guilds.Add(new Guild
        {
            DiscordId = 222,
            Name = "Server Two"
        });
        await testDb.Db.SaveChangesAsync();

        McpService service = CreateService(testDb.Db);

        IReadOnlyList<McpServerItem> guilds = await service.GetGuildsAsync();

        Assert.Equal(2, guilds.Count);
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsPaginatedUsers()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        await SeedBaseAsync(testDb.Db);

        McpService service = CreateService(testDb.Db);

        IReadOnlyList<McpUserItem> users = await service.GetUsersAsync(page: 1, limit: 10);

        Assert.Single(users);
        Assert.Equal(1000m, users[0].Balance);
    }

    [Fact]
    public async Task GetQuotesAsync_ReturnsQuotes()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);

        testDb.Db.Quotes.Add(new Quote
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = "Hello world",
            Approved = true
        });
        testDb.Db.Quotes.Add(new Quote
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = "Second quote",
            Approved = false
        });
        await testDb.Db.SaveChangesAsync();

        McpService service = CreateService(testDb.Db);

        // Get approved only
        McpQuotePage page = await service.GetQuotesAsync(approvedOnly: true);

        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
        Assert.Equal("Hello world", page.Items[0].Content);

        // Get all (including pending)
        McpQuotePage allPage = await service.GetQuotesAsync(approvedOnly: false);

        Assert.Equal(2, allPage.Total);
    }

    [Fact]
    public async Task GetQuoteByIdAsync_ReturnsQuote()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);

        Quote quote = new()
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = "Test quote detail",
            Approved = true
        };
        testDb.Db.Quotes.Add(quote);
        await testDb.Db.SaveChangesAsync();

        McpService service = CreateService(testDb.Db);

        McpQuoteDetail? detail = await service.GetQuoteByIdAsync(quote.Id);

        Assert.NotNull(detail);
        Assert.Equal(quote.Id, detail.Id);
        Assert.Equal("Test quote detail", detail.Content);
        Assert.Equal(user.Username, detail.Author);
    }

    [Fact]
    public async Task GetQuoteByIdAsync_ReturnsNull_WhenNotFound()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        McpService service = CreateService(testDb.Db);

        McpQuoteDetail? detail = await service.GetQuoteByIdAsync(999);

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetRecentLogsAsync_ReturnsLogs()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();

        testDb.Db.Logs.Add(new Log { Message = "info log", Severity = (int)Discord.LogSeverity.Info, InsertDate = DateTime.UtcNow });
        testDb.Db.Logs.Add(new Log { Message = "warning log", Severity = (int)Discord.LogSeverity.Warning, InsertDate = DateTime.UtcNow });
        testDb.Db.Logs.Add(new Log { Message = "error log", Severity = (int)Discord.LogSeverity.Error, InsertDate = DateTime.UtcNow });
        await testDb.Db.SaveChangesAsync();

        McpService service = CreateService(testDb.Db);

        IReadOnlyList<McpModerationEntry> logs = await service.GetRecentLogsAsync(limit: 10);

        Assert.Equal(3, logs.Count);

        // Filter by severity
        IReadOnlyList<McpModerationEntry> errorLogs = await service.GetRecentLogsAsync(limit: 10, severity: "Error");
        Assert.Single(errorLogs);
        Assert.Equal("error log", errorLogs[0].Message);
    }

    [Fact]
    public async Task GetLeaderboardAsync_ReturnsRankings()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);

        testDb.Db.UserActivity.Add(new UserActivity
        {
            GuildId = guild.Id,
            UserId = user.Id,
            DiscordChannelId = 1,
            DiscordMessageId = 2,
            XpGained = 100,
            MessageLength = 25,
            InsertDate = DateTime.UtcNow.AddHours(-1)
        });
        await testDb.Db.SaveChangesAsync();

        McpService service = CreateService(testDb.Db);

        IReadOnlyList<McpLeaderboardEntry> leaderboard = await service.GetLeaderboardAsync(
            metric: "xp", guildId: guild.Id, days: 30, limit: 10);

        Assert.Single(leaderboard);
        Assert.Equal(1, leaderboard[0].Rank);
        Assert.Equal(user.Id, leaderboard[0].UserId);
        Assert.Equal(100, leaderboard[0].Value);
    }

    [Fact]
    public async Task GetStockSummaryAsync_ReturnsSummary()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild _, User user) = await SeedBaseAsync(testDb.Db);

        testDb.Db.Stocks.Add(new Stock
        {
            EntityType = Database.Enums.StockEntityType.User,
            EntityId = user.Id,
            Price = 120m,
            PreviousPrice = 100m,
            DailyChangePercent = 20m
        });
        testDb.Db.Stocks.Add(new Stock
        {
            EntityType = Database.Enums.StockEntityType.Guild,
            EntityId = 2,
            Price = 80m,
            PreviousPrice = 100m,
            DailyChangePercent = -20m
        });
        testDb.Db.Stocks.Add(new Stock
        {
            EntityType = Database.Enums.StockEntityType.Guild,
            EntityId = 3,
            Price = 50m,
            PreviousPrice = 100m,
            DailyChangePercent = -50m
        });
        await testDb.Db.SaveChangesAsync();

        McpService service = CreateService(testDb.Db);

        McpStockSummary summary = await service.GetStockSummaryAsync(moverLimit: 5);

        Assert.Equal(3, summary.TotalStocks);
        Assert.Single(summary.TopGainers);
        Assert.Equal(2, summary.TopLosers.Count);
        Assert.Equal(20m, summary.TopGainers[0].DailyChangePercent);
        Assert.Equal(-50m, summary.TopLosers[0].DailyChangePercent);
    }

    // ── Test Infrastructure ──

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

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;

        DB db = new(options);
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
        db.Guilds.Add(guild);
        await db.SaveChangesAsync();

        User user = new()
        {
            DiscordId = 456,
            Username = "TestUser",
            Balance = 1000m
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (guild, user);
    }

    private static McpService CreateService(DB db) => new(db);
}