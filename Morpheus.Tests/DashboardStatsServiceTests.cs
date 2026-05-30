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

    [Fact]
    public async Task GetActivityLeaderboardAsync_AppliesChannelFilter()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User firstUser) = await SeedBaseAsync(testDb.Db);
        User secondUser = new()
        {
            DiscordId = 333,
            Username = "outside-channel"
        };
        testDb.Db.Users.Add(secondUser);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.UserActivity.AddRange(
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = firstUser.Id,
                DiscordChannelId = 555,
                DiscordMessageId = 1,
                XpGained = 20,
                InsertDate = DateTime.UtcNow.AddMinutes(-2)
            },
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = secondUser.Id,
                DiscordChannelId = 777,
                DiscordMessageId = 2,
                XpGained = 50,
                InsertDate = DateTime.UtcNow.AddMinutes(-1)
            });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardLeaderboardResponse leaderboard = await service.GetActivityLeaderboardAsync(
            guild.Id,
            "messages",
            days: 7,
            limit: 10,
            channelId: "555");

        DashboardLeaderboardItem item = Assert.Single(leaderboard.Items);
        Assert.Equal(firstUser.Id, item.UserId);
        Assert.Equal(1, item.Value);
    }

    [Fact]
    public async Task GetInsightsAsync_ReturnsCrossDomainAnalytics()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);
        Channel channel = new()
        {
            DiscordId = 555,
            Name = "general"
        };
        testDb.Db.Channels.Add(channel);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.UserLevels.Add(new UserLevels
        {
            GuildId = guild.Id,
            UserId = user.Id,
            TotalXp = 1000,
            UserMessageCount = 1
        });
        testDb.Db.UserActivity.Add(new UserActivity
        {
            GuildId = guild.Id,
            UserId = user.Id,
            DiscordChannelId = channel.DiscordId,
            DiscordMessageId = 42,
            XpGained = 25,
            MessageLength = 40,
            InsertDate = DateTime.UtcNow
        });
        testDb.Db.Quotes.Add(new Quote
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = "insight quote",
            Approved = true,
            InsertDate = DateTime.UtcNow
        });
        testDb.Db.StockTransactions.Add(new StockTransaction
        {
            UserId = user.Id,
            Type = TransactionType.SlotsWin,
            Amount = 50m,
            Fee = 1m,
            InsertDate = DateTime.UtcNow
        });
        testDb.Db.ButtonGamePresses.Add(new ButtonGamePress
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Score = 75,
            InsertDate = DateTime.UtcNow
        });
        testDb.Db.Reminders.Add(new Reminder
        {
            GuildId = guild.Id,
            UserId = user.Id,
            ChannelId = channel.DiscordId,
            Text = "test reminder",
            DueDate = DateTime.UtcNow.AddHours(1)
        });
        testDb.Db.TemporaryBans.Add(new TemporaryBan
        {
            GuildId = guild.DiscordId,
            UserId = user.DiscordId,
            Reason = "test",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await testDb.Db.SaveChangesAsync();

        Stock stock = new()
        {
            EntityType = StockEntityType.Channel,
            EntityId = channel.Id,
            Price = 10m,
            DailyChangePercent = 2m
        };
        testDb.Db.Stocks.Add(stock);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.StockHoldings.Add(new StockHolding
        {
            UserId = user.Id,
            StockId = stock.Id,
            Shares = 3m,
            TotalInvested = 25m
        });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardInsightsResponse insights = await service.GetInsightsAsync(
            guild.Id,
            user.Id,
            channel.DiscordId.ToString(),
            days: 7,
            scope: "channel",
            sortDirection: "desc",
            minActivity: 1);

        Assert.Equal("channel", insights.Scope);
        Assert.Equal(1, insights.Activity.Messages);
        Assert.Equal("general", Assert.Single(insights.Channels).Name);
        DashboardUserActivitySummary userSummary = Assert.Single(insights.Users);
        Assert.Equal(user.Username, userSummary.Username);
        Assert.Equal(1, userSummary.Level);
        Assert.Equal(1, insights.Quotes.Approved);
        Assert.Equal(50m, insights.Economy.TransactionVolume);
        Assert.Equal(1, insights.Stocks.Stocks);
        Assert.Equal(1, insights.ButtonGame.Presses);
        Assert.Equal(1, insights.Operations.Reminders.Pending);
        Assert.Equal(1, insights.Operations.Moderation.PendingTemporaryBans);
    }

    [Fact]
    public async Task GetInsightsAsync_GlobalScopeIgnoresStaleEntityFilters()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User firstUser) = await SeedBaseAsync(testDb.Db);
        User secondUser = new()
        {
            DiscordId = 444,
            Username = "included-global"
        };
        testDb.Db.Users.Add(secondUser);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.UserActivity.AddRange(
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = firstUser.Id,
                DiscordChannelId = 555,
                DiscordMessageId = 1,
                XpGained = 25,
                InsertDate = DateTime.UtcNow
            },
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = secondUser.Id,
                DiscordChannelId = 777,
                DiscordMessageId = 2,
                XpGained = 30,
                InsertDate = DateTime.UtcNow
            });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardInsightsResponse insights = await service.GetInsightsAsync(
            guild.Id,
            firstUser.Id,
            "555",
            days: 7,
            scope: "global",
            sortDirection: "desc",
            minActivity: 1);

        Assert.Equal("global", insights.Scope);
        Assert.Null(insights.GuildId);
        Assert.Null(insights.UserId);
        Assert.Null(insights.ChannelId);
        Assert.Equal(2, insights.Activity.Messages);
        Assert.Contains(insights.Users, user => user.UserId == secondUser.Id);
    }

    [Fact]
    public async Task GetInsightsAsync_FilterOptionsIgnoreVisibleInsightCaps()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User firstUser) = await SeedBaseAsync(testDb.Db);
        User quietUser = new()
        {
            DiscordId = 555,
            Username = "quiet"
        };
        Channel channel = new()
        {
            DiscordId = 777,
            Name = "low-traffic"
        };
        testDb.Db.Users.Add(quietUser);
        testDb.Db.Channels.Add(channel);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.UserLevels.AddRange(
            new UserLevels
            {
                GuildId = guild.Id,
                UserId = firstUser.Id,
                TotalXp = 10,
                UserMessageCount = 1
            },
            new UserLevels
            {
                GuildId = guild.Id,
                UserId = quietUser.Id,
                TotalXp = 0,
                UserMessageCount = 0
            });
        testDb.Db.UserActivity.Add(new UserActivity
        {
            GuildId = guild.Id,
            UserId = firstUser.Id,
            DiscordChannelId = channel.DiscordId,
            DiscordMessageId = 1,
            XpGained = 10,
            InsertDate = DateTime.UtcNow
        });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardInsightsResponse insights = await service.GetInsightsAsync(
            guild.Id,
            userId: null,
            channelId: null,
            days: 7,
            scope: "server",
            sortDirection: "desc",
            minActivity: 2);

        Assert.Empty(insights.Users);
        Assert.Empty(insights.Channels);
        Assert.Contains(insights.FilterOptions.Users, user => user.UserId == firstUser.Id);
        Assert.Contains(insights.FilterOptions.Users, user => user.UserId == quietUser.Id);
        Assert.Contains(insights.FilterOptions.Channels, option => option.DiscordId == channel.DiscordId.ToString());
    }

    [Fact]
    public async Task GetInsightsAsync_UserEconomyIncludesIncomingTargetTransactions()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User receiver) = await SeedBaseAsync(testDb.Db);
        User sender = new()
        {
            DiscordId = 666,
            Username = "sender"
        };
        testDb.Db.Users.Add(sender);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.StockTransactions.Add(new StockTransaction
        {
            UserId = sender.Id,
            TargetUserId = receiver.Id,
            Type = TransactionType.Transfer,
            Amount = 75m,
            Fee = 1m,
            InsertDate = DateTime.UtcNow
        });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardInsightsResponse insights = await service.GetInsightsAsync(
            guild.Id,
            receiver.Id,
            channelId: null,
            days: 7,
            scope: "user",
            sortDirection: "desc",
            minActivity: 1);

        Assert.Equal(75m, insights.Economy.TransactionVolume);
        Assert.Equal(0m, insights.Economy.Fees);
        Assert.Equal(1, insights.Economy.ActiveTraders);
        Assert.Equal(75m, insights.Economy.DailyFlow.Sum(point => point.Inflow));
        Assert.Equal(0m, insights.Economy.DailyFlow.Sum(point => point.Outflow));
        DashboardMoneyFlow flow = Assert.Single(insights.Economy.MoneyFlows);
        Assert.Equal("Member transfers", flow.Source);
        Assert.Equal("Wallets", flow.Target);
        Assert.Equal(75m, flow.Value);
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
