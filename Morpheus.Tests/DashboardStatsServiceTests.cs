using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Dashboard;
using Morpheus.Database;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;

namespace Morpheus.Tests;

public class DashboardStatsServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_ReturnsDashboardAggregates()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);

        testDb.Db.UserLevels.Add(new UserLevels
        {
            GuildId = guild.Id,
            UserId = user.Id,
            TotalXp = 150,
            UserMessageCount = 3
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
        Quote quote = new()
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = "dashboard quote",
            Approved = true
        };
        testDb.Db.Quotes.Add(quote);
        testDb.Db.Logs.Add(new Log { Message = "hello", InsertDate = DateTime.UtcNow });
        await testDb.Db.SaveChangesAsync();

        testDb.Db.QuoteScores.Add(new QuoteScore { QuoteId = quote.Id, UserId = user.Id, Score = 4 });
        testDb.Db.Stocks.Add(new Stock { EntityType = StockEntityType.User, EntityId = user.Id, Price = 12m });
        await testDb.Db.SaveChangesAsync();

        Stock stock = await testDb.Db.Stocks.SingleAsync();
        testDb.Db.StockHoldings.Add(new StockHolding
        {
            StockId = stock.Id,
            UserId = user.Id,
            Shares = 2m,
            TotalInvested = 20m
        });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardOverviewResponse overview = await service.GetOverviewAsync();

        Assert.Equal(1, overview.System.Guilds);
        Assert.Equal(1, overview.System.Users);
        Assert.Equal(1, overview.System.Stocks);
        Assert.Equal(3, overview.Activity.TotalMessages);
        Assert.Equal(150, overview.Activity.TotalXp);
        Assert.Equal(1, overview.Activity.ActiveUsersLast30Days);
        Assert.Equal(1, overview.Activity.MessagesLast30Days);
        Assert.Equal(50, overview.Activity.XpLast30Days);
        Assert.Equal(1, overview.Quotes.Approved);
        Assert.Equal(4, overview.Quotes.TotalScores);
        Assert.Equal(1000m, overview.Economy.TotalBalance);
        Assert.Equal(24m, overview.Economy.StockPortfolioValue);
        Assert.Equal(1, overview.Logs.Total);
        Assert.Equal(1, overview.Logs.Last24Hours);
    }

    [Fact]
    public async Task GetActivitySeriesAsync_FillsMissingDays()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);

        testDb.Db.UserActivity.Add(new UserActivity
        {
            GuildId = guild.Id,
            UserId = user.Id,
            DiscordChannelId = 1,
            DiscordMessageId = 2,
            XpGained = 10,
            MessageLength = 40,
            InsertDate = DateTime.UtcNow.Date
        });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardActivitySeriesResponse series = await service.GetActivitySeriesAsync(guild.Id, days: 3);

        Assert.Equal(3, series.Points.Count);
        Assert.Equal(0, series.Points[0].Messages);
        Assert.Equal(0, series.Points[1].Messages);
        Assert.Equal(1, series.Points[2].Messages);
        Assert.Equal(10, series.Points[2].Xp);
        Assert.Equal(40.0, series.Points[2].AverageMessageLength);
    }

    [Fact]
    public async Task GetActivityLeaderboardAsync_ReturnsRankedRecentUsers()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User firstUser) = await SeedBaseAsync(testDb.Db);
        User secondUser = new()
        {
            DiscordId = 222,
            Username = "second"
        };
        testDb.Db.Users.Add(secondUser);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.UserActivity.AddRange(
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = firstUser.Id,
                DiscordChannelId = 1,
                DiscordMessageId = 1,
                XpGained = 20,
                InsertDate = DateTime.UtcNow.AddHours(-2)
            },
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = secondUser.Id,
                DiscordChannelId = 1,
                DiscordMessageId = 2,
                XpGained = 50,
                InsertDate = DateTime.UtcNow.AddHours(-1)
            });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardLeaderboardResponse leaderboard = await service.GetActivityLeaderboardAsync(
            guild.Id,
            "xp",
            days: 7,
            limit: 10);

        Assert.Equal("xp", leaderboard.Metric);
        Assert.Equal(2, leaderboard.Items.Count);
        Assert.Equal("second", leaderboard.Items[0].Username);
        Assert.Equal(50, leaderboard.Items[0].Value);
        Assert.Equal(1, leaderboard.Items[0].Rank);
        Assert.NotNull(leaderboard.Items[0].Level);
    }

    private static DashboardStatsService CreateService(DB db) =>
        new(db, new DashboardApiOptions(
            "http://127.0.0.1:5267",
            ["http://localhost:3000"],
            string.Empty,
            90));

    private static async Task<(Guild Guild, User User)> SeedBaseAsync(DB db)
    {
        Guild guild = new()
        {
            DiscordId = 111,
            Name = "Test guild"
        };
        User user = new()
        {
            DiscordId = 123,
            Username = "tester",
            Balance = 1000m
        };

        db.Guilds.Add(guild);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (guild, user);
    }

    private static async Task<SqliteTestDb> CreateSqliteDbAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        return new SqliteTestDb(connection, db);
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
}
