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
    public async Task GetGlobalOverviewAsync_ReturnsGlobalTotalsHighlightsAndFeeds()
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
            TotalXp = 150,
            UserMessageCount = 3
        });
        testDb.Db.UserActivity.Add(new UserActivity
        {
            GuildId = guild.Id,
            UserId = user.Id,
            DiscordChannelId = channel.DiscordId,
            DiscordMessageId = 2,
            XpGained = 50,
            MessageLength = 25,
            InsertDate = DateTime.UtcNow
        });
        Quote quote = new()
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = "global quote",
            Approved = true
        };
        testDb.Db.Quotes.Add(quote);
        testDb.Db.Logs.Add(new Log
        {
            Message = "careful now",
            Severity = 2,
            InsertDate = DateTime.UtcNow
        });
        testDb.Db.BotSettings.AddRange(
            new BotSetting { Key = "ubi_pool", Value = "123.45" },
            new BotSetting { Key = "slots_vault", Value = "987.65" });
        testDb.Db.ButtonGamePresses.Add(new ButtonGamePress
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Score = 10
        });
        testDb.Db.Reminders.Add(new Reminder
        {
            GuildId = guild.Id,
            UserId = user.Id,
            ChannelId = channel.DiscordId,
            Text = "global reminder",
            DueDate = DateTime.UtcNow.AddHours(1)
        });
        await testDb.Db.SaveChangesAsync();

        testDb.Db.QuoteScores.Add(new QuoteScore { QuoteId = quote.Id, UserId = user.Id, Score = 4 });
        testDb.Db.QuoteApprovalMessages.Add(new QuoteApprovalMessage
        {
            QuoteId = quote.Id,
            ApprovalMessageId = 999,
            Approved = false,
            Type = QuoteApprovalType.AddRequest
        });
        Stock stock = new()
        {
            EntityType = StockEntityType.User,
            EntityId = user.Id,
            Price = 12m,
            DailyChangePercent = 3m
        };
        testDb.Db.Stocks.Add(stock);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.StockHoldings.Add(new StockHolding
        {
            StockId = stock.Id,
            UserId = user.Id,
            Shares = 2m,
            TotalInvested = 20m
        });
        testDb.Db.StockTransactions.Add(new StockTransaction
        {
            UserId = user.Id,
            StockId = stock.Id,
            Type = TransactionType.StockBuy,
            Amount = 25m,
            Fee = 1m,
            InsertDate = DateTime.UtcNow
        });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardGlobalOverviewResponse overview = await service.GetGlobalOverviewAsync(7);

        Assert.Equal(1, overview.Totals.TotalServers);
        Assert.Equal(1, overview.Totals.TotalKnownUsers);
        Assert.Equal(3, overview.Totals.TotalTrackedMessages);
        Assert.Equal(150, overview.Totals.TotalXpGenerated);
        Assert.Equal(1, overview.Totals.LatestDayMessages);
        Assert.Equal(50, overview.Totals.LatestDayXpGenerated);
        Assert.Equal(1, overview.Totals.TotalQuotes);
        Assert.Equal(1, overview.Totals.TotalApprovedQuotes);
        Assert.Equal(1, overview.Totals.PendingQuoteApprovals);
        Assert.Equal(1000m, overview.Totals.TotalEconomyBalance);
        Assert.Equal(1024m, overview.Totals.TotalEstimatedNetWorth);
        Assert.Equal(123.45m, overview.Totals.UbiPoolSize);
        Assert.Equal(987.65m, overview.Totals.SlotsVaultSize);
        Assert.Equal(1, overview.Totals.TotalTransactions);
        Assert.Equal(1, overview.Totals.TotalButtonPresses);
        Assert.Equal(1, overview.Totals.ActiveReminders);
        Assert.Equal(1, overview.Totals.RecentWarningsOrErrors);
        Assert.Single(overview.Highlights.MostActiveServersToday);
        Assert.Single(overview.Highlights.BiggestXpGainers);
        Assert.Single(overview.Highlights.MostPopularQuotes);
        Assert.Equal(7, overview.Visuals.Activity.Count);
        Assert.Single(overview.Visuals.TransactionTypes);
        Assert.Single(overview.Feeds.RecentEconomyEvents);
        Assert.Single(overview.Feeds.RecentBotHealthEvents);
    }

    [Fact]
    public async Task GetGlobalOverviewAsync_ViewActivityReturnsOnlyActivitySlice()
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

        DashboardGlobalOverviewResponse overview = await service.GetGlobalOverviewAsync(7, "activity");

        Assert.Equal(0, overview.Totals.TotalServers);
        Assert.Equal(7, overview.Visuals.Activity.Count);
        Assert.Single(overview.Visuals.CalendarActivity, cell => cell.Messages > 0);
        Assert.Empty(overview.Highlights.MostActiveServersToday);
        Assert.Empty(overview.Feeds.RecentEconomyEvents);
    }

    [Fact]
    public async Task GetInsightsAsync_ActivityViewReturnsMainActivityAnalytics()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild firstGuild, User firstUser) = await SeedBaseAsync(testDb.Db);
        Guild secondGuild = new()
        {
            DiscordId = 222,
            Name = "Second guild"
        };
        User secondUser = new()
        {
            DiscordId = 456,
            Username = "second-user",
            Balance = 500m
        };
        Channel general = new()
        {
            DiscordId = 1001,
            Name = "general"
        };
        Channel market = new()
        {
            DiscordId = 1002,
            Name = "market"
        };
        testDb.Db.Guilds.Add(secondGuild);
        testDb.Db.Users.Add(secondUser);
        testDb.Db.Channels.AddRange(general, market);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.UserLevels.AddRange(
            new UserLevels
            {
                GuildId = firstGuild.Id,
                UserId = firstUser.Id,
                TotalXp = 1200,
                UserMessageCount = 40,
                UserAverageMessageLength = 48,
                UserAverageMessageLengthEma = 51
            },
            new UserLevels
            {
                GuildId = secondGuild.Id,
                UserId = secondUser.Id,
                TotalXp = 880,
                UserMessageCount = 34,
                UserAverageMessageLength = 76,
                UserAverageMessageLengthEma = 70
            });

        DateTime today = DateTime.UtcNow.Date;
        for (int offset = 0; offset < 6; offset++)
        {
            testDb.Db.UserActivity.Add(new UserActivity
            {
                GuildId = firstGuild.Id,
                UserId = firstUser.Id,
                DiscordChannelId = general.DiscordId,
                DiscordMessageId = (ulong)(100 + offset),
                XpGained = 12 + offset,
                MessageLength = 35 + offset * 4,
                InsertDate = today.AddDays(-offset).AddHours(18 + offset % 3)
            });
            testDb.Db.UserActivity.Add(new UserActivity
            {
                GuildId = secondGuild.Id,
                UserId = secondUser.Id,
                DiscordChannelId = market.DiscordId,
                DiscordMessageId = (ulong)(200 + offset),
                XpGained = 20 + offset * 2,
                MessageLength = 60 + offset * 8,
                InsertDate = today.AddDays(-offset).AddHours(20)
            });
        }
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardInsightsResponse insights = await service.GetInsightsAsync(
            null,
            null,
            null,
            days: 7,
            scope: "global",
            sortDirection: "desc",
            minActivity: 1,
            view: "activity",
            startDateUtc: today.AddDays(-6),
            endDateUtc: today);

        DashboardActivityAnalytics analytics = insights.ActivityAnalytics;
        Assert.Equal(12, insights.Activity.Messages);
        Assert.NotEmpty(analytics.ComparisonSeries);
        Assert.Contains(analytics.ComparisonSeries, series => series.Kind == "time-range");
        Assert.NotEmpty(analytics.XpByUser);
        Assert.NotEmpty(analytics.XpByChannel);
        Assert.NotEmpty(analytics.XpByServer);
        Assert.NotEmpty(analytics.MessageLengthHistogram);
        Assert.NotEmpty(analytics.MessageLengthTrend);
        Assert.NotEmpty(analytics.MessageLengthBoxPlots);
        Assert.NotEmpty(analytics.MessageCountVsXp);
        Assert.NotEmpty(analytics.ChannelHourHeatmap);
        Assert.NotEmpty(analytics.ServerDayHeatmap);
        Assert.NotEmpty(analytics.ChannelDayHeatmap);
        Assert.NotEmpty(analytics.UserContributionPareto);
        Assert.Contains(analytics.Leaderboards, board => board.Key == "global-xp");
        Assert.Contains(analytics.Leaderboards, board => board.Key == "recent-messages");
        Assert.NotEmpty(analytics.DailyActiveUsers);
        Assert.NotEmpty(analytics.WeeklyActiveUsers);
        Assert.NotEmpty(analytics.MonthlyActiveUsers);
    }

    [Fact]
    public async Task GetInsightsAsync_QuotesViewReturnsQuoteAnalyticsAndManagementQueues()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User author) = await SeedBaseAsync(testDb.Db);
        guild.UseGlobalQuotes = true;
        guild.QuotesApprovalChannelId = 999;
        guild.QuoteAddRequiredApprovals = 2;
        guild.QuoteRemoveRequiredApprovals = 2;
        User voter = new()
        {
            DiscordId = 456,
            Username = "voter"
        };
        User reviewer = new()
        {
            DiscordId = 789,
            Username = "reviewer"
        };
        Guild weakGuild = new()
        {
            DiscordId = 222,
            Name = "Weak setup",
            QuoteAddRequiredApprovals = 1,
            QuoteRemoveRequiredApprovals = 1
        };
        testDb.Db.Users.AddRange(voter, reviewer);
        testDb.Db.Guilds.Add(weakGuild);
        await testDb.Db.SaveChangesAsync();

        DateTime today = DateTime.UtcNow.Date;
        DateTime start = today.AddDays(-9);
        Quote topQuote = new()
        {
            GuildId = guild.Id,
            UserId = author.Id,
            Content = "top quote",
            Approved = true,
            InsertDate = start.AddDays(2)
        };
        Quote lowQuote = new()
        {
            GuildId = guild.Id,
            UserId = author.Id,
            Content = "low quote",
            Approved = true,
            InsertDate = start.AddDays(3)
        };
        Quote freshPendingQuote = new()
        {
            GuildId = guild.Id,
            UserId = author.Id,
            Content = "fresh pending quote",
            Approved = false,
            InsertDate = today.AddDays(-1)
        };
        Quote expiredPendingQuote = new()
        {
            GuildId = guild.Id,
            UserId = author.Id,
            Content = "expired pending quote",
            Approved = false,
            InsertDate = start.AddDays(1)
        };
        Quote removedQuote = new()
        {
            GuildId = guild.Id,
            UserId = author.Id,
            Content = "removed quote",
            Approved = true,
            Removed = true,
            InsertDate = start.AddDays(4)
        };
        testDb.Db.Quotes.AddRange(topQuote, lowQuote, freshPendingQuote, expiredPendingQuote, removedQuote);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.QuoteScores.AddRange(
            new QuoteScore { QuoteId = topQuote.Id, UserId = voter.Id, Score = 8, InsertDate = start.AddDays(2).AddHours(1) },
            new QuoteScore { QuoteId = topQuote.Id, UserId = reviewer.Id, Score = -2, InsertDate = start.AddDays(2).AddHours(2) },
            new QuoteScore { QuoteId = lowQuote.Id, UserId = voter.Id, Score = -5, InsertDate = start.AddDays(3).AddHours(1) },
            new QuoteScore { QuoteId = removedQuote.Id, UserId = reviewer.Id, Score = -3, InsertDate = start.AddDays(4).AddHours(1) });
        QuoteApprovalMessage completedApproval = new()
        {
            QuoteId = topQuote.Id,
            ApprovalMessageId = 1001,
            Approved = true,
            Type = QuoteApprovalType.AddRequest,
            InsertDate = start.AddDays(2)
        };
        QuoteApprovalMessage freshApproval = new()
        {
            QuoteId = freshPendingQuote.Id,
            ApprovalMessageId = 1002,
            Approved = false,
            Type = QuoteApprovalType.AddRequest,
            InsertDate = today.AddDays(-1)
        };
        QuoteApprovalMessage expiredApproval = new()
        {
            QuoteId = expiredPendingQuote.Id,
            ApprovalMessageId = 1003,
            Approved = false,
            Type = QuoteApprovalType.AddRequest,
            InsertDate = start.AddDays(1)
        };
        testDb.Db.QuoteApprovalMessages.AddRange(completedApproval, freshApproval, expiredApproval);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.QuoteApprovals.AddRange(
            new QuoteApproval { QuoteApprovalMessageId = completedApproval.Id, UserId = (ulong)voter.Id, InsertDate = start.AddDays(2).AddHours(1) },
            new QuoteApproval { QuoteApprovalMessageId = completedApproval.Id, UserId = (ulong)reviewer.Id, InsertDate = start.AddDays(2).AddHours(3) },
            new QuoteApproval { QuoteApprovalMessageId = freshApproval.Id, UserId = (ulong)reviewer.Id, InsertDate = today.AddDays(-1).AddHours(1) });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardInsightsResponse insights = await service.GetInsightsAsync(
            guildId: null,
            userId: null,
            channelId: null,
            days: 10,
            scope: "global",
            sortDirection: "desc",
            minActivity: 0,
            view: "quotes",
            startDateUtc: start,
            endDateUtc: today);

        DashboardQuoteInsights quotes = insights.Quotes;
        Assert.Equal(5, quotes.Total);
        Assert.Equal(2, quotes.Approved);
        Assert.Equal(2, quotes.Pending);
        Assert.Equal(1, quotes.Removed);
        Assert.Equal(3, quotes.ApprovalRequests);
        Assert.Equal(1, quotes.CompletedApprovalRequests);
        Assert.Equal(1, quotes.PendingApprovalRequests);
        Assert.Equal(1, quotes.ExpiredApprovalRequests);
        Assert.NotEmpty(quotes.CreationTimeline);
        Assert.NotEmpty(quotes.ScoreTrend);
        Assert.NotEmpty(quotes.ApprovalActivityCalendar);
        Assert.Contains(quotes.HighestScoringQuotes, quote => quote.Id == topQuote.Id);
        Assert.Contains(quotes.LowestScoringQuotes, quote => quote.Id == lowQuote.Id);
        Assert.Contains(quotes.MostRemovedQuotes, quote => quote.Id == removedQuote.Id);
        Assert.Contains(quotes.PendingApprovalQueue, request => request.Id == freshApproval.Id);
        Assert.Contains(quotes.ExpiredApprovalQueue, request => request.Id == expiredApproval.Id);
        Assert.Contains(quotes.RemovedQuoteList, quote => quote.Id == removedQuote.Id);
        Assert.Contains(quotes.TopVoters, vote => vote.Username == voter.Username);
        Assert.Contains(quotes.ApprovalVoters, vote => vote.Username == reviewer.Username);
        Assert.Contains(quotes.ServerSummaries, summary => summary.GuildId == guild.Id && summary.Total == 5);
        Assert.Contains(quotes.SetupSummaries, setup => setup.GuildId == weakGuild.Id && setup.Health == "Missing");

        DashboardQuoteDetailsResponse? detail = await service.GetQuoteDetailsAsync(topQuote.Id);
        Assert.NotNull(detail);
        Assert.Equal(author.Id, detail.UserId);
        Assert.NotEmpty(detail.Voters);
        Assert.NotEmpty(detail.ApprovalRequests);
    }

    [Fact]
    public async Task GetInsightsAsync_EconomyAndStocksViewsReturnAnalyticsTerminalMetrics()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User firstUser) = await SeedBaseAsync(testDb.Db);
        User secondUser = new()
        {
            DiscordId = 456,
            Username = "second",
            Balance = 5000m
        };
        Channel channel = new()
        {
            DiscordId = 9001,
            Name = "market"
        };
        testDb.Db.Users.Add(secondUser);
        testDb.Db.Channels.Add(channel);
        testDb.Db.BotSettings.AddRange(
            new BotSetting { Key = "ubi_pool", Value = "250.00" },
            new BotSetting { Key = "slots_vault", Value = "12000.00" });
        await testDb.Db.SaveChangesAsync();

        DateTime today = DateTime.UtcNow.Date;
        DateTime start = today.AddDays(-6);
        Stock userStock = new()
        {
            EntityType = StockEntityType.User,
            EntityId = firstUser.Id,
            Price = 120m,
            PreviousPrice = 100m,
            DailyChangePercent = 20m,
            InsertDate = start
        };
        Stock guildStock = new()
        {
            EntityType = StockEntityType.Guild,
            EntityId = guild.Id,
            Price = 80m,
            PreviousPrice = 100m,
            DailyChangePercent = -20m,
            InsertDate = start.AddDays(1)
        };
        Stock channelStock = new()
        {
            EntityType = StockEntityType.Channel,
            EntityId = channel.Id,
            Price = 150m,
            PreviousPrice = 140m,
            DailyChangePercent = 7.14m,
            InsertDate = start.AddDays(2)
        };
        testDb.Db.Stocks.AddRange(userStock, guildStock, channelStock);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.StockHoldings.AddRange(
            new StockHolding { UserId = firstUser.Id, StockId = userStock.Id, Shares = 2m, TotalInvested = 180m },
            new StockHolding { UserId = secondUser.Id, StockId = guildStock.Id, Shares = 3m, TotalInvested = 270m },
            new StockHolding { UserId = secondUser.Id, StockId = channelStock.Id, Shares = 1m, TotalInvested = 120m });
        testDb.Db.UserActivity.AddRange(
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = firstUser.Id,
                DiscordChannelId = channel.DiscordId,
                DiscordMessageId = 1,
                XpGained = 20,
                MessageLength = 40,
                InsertDate = start.AddDays(1)
            },
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = secondUser.Id,
                DiscordChannelId = channel.DiscordId,
                DiscordMessageId = 2,
                XpGained = 30,
                MessageLength = 60,
                InsertDate = start.AddDays(2)
            });
        await testDb.Db.SaveChangesAsync();

        testDb.Db.StockTransactions.AddRange(
            new StockTransaction { UserId = firstUser.Id, StockId = userStock.Id, Type = TransactionType.StockBuy, Amount = 100m, Fee = 1m, Shares = 1m, PriceAtTransaction = 100m, InsertDate = start.AddDays(1) },
            new StockTransaction { UserId = firstUser.Id, StockId = userStock.Id, Type = TransactionType.StockSell, Amount = 60m, Fee = 6m, Shares = 0.5m, PriceAtTransaction = 120m, InsertDate = start.AddDays(2) },
            new StockTransaction { UserId = firstUser.Id, TargetUserId = secondUser.Id, StockId = guildStock.Id, Type = TransactionType.StockTransfer, Amount = 25m, Fee = 0m, Shares = 0.25m, PriceAtTransaction = 80m, InsertDate = start.AddDays(2) },
            new StockTransaction { UserId = firstUser.Id, TargetUserId = secondUser.Id, Type = TransactionType.Transfer, Amount = 50m, Fee = 2.5m, InsertDate = start.AddDays(2) },
            new StockTransaction { UserId = secondUser.Id, Type = TransactionType.Donation, Amount = 30m, Fee = 0m, InsertDate = start.AddDays(3) },
            new StockTransaction { UserId = firstUser.Id, TargetUserId = secondUser.Id, Type = TransactionType.RobberyWin, Amount = 40m, Fee = 0m, InsertDate = start.AddDays(3) },
            new StockTransaction { UserId = firstUser.Id, TargetUserId = secondUser.Id, Type = TransactionType.RobberyLoss, Amount = 10m, Fee = 0m, InsertDate = start.AddDays(4) },
            new StockTransaction { UserId = secondUser.Id, Type = TransactionType.SlotsWin, Amount = 90m, Fee = 4m, InsertDate = start.AddDays(4) },
            new StockTransaction { UserId = secondUser.Id, Type = TransactionType.SlotsLoss, Amount = 45m, Fee = 0m, InsertDate = start.AddDays(5) });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardInsightsResponse economyInsights = await service.GetInsightsAsync(
            guildId: null,
            userId: null,
            channelId: null,
            days: 7,
            scope: "global",
            sortDirection: "desc",
            minActivity: 0,
            view: "economy",
            startDateUtc: start,
            endDateUtc: today);

        Assert.Equal(9, economyInsights.Economy.TransactionCount);
        Assert.Equal(250m, economyInsights.Economy.UbiPoolSize);
        Assert.Equal(12000m, economyInsights.Economy.SlotsVaultSize);
        Assert.Equal(30m, economyInsights.Economy.UbiDonations);
        Assert.Equal(10m, economyInsights.Economy.TaxesCollected);
        Assert.Equal(1, economyInsights.Economy.RobberyWins);
        Assert.Equal(1, economyInsights.Economy.RobberyLosses);
        Assert.NotEmpty(economyInsights.Economy.MoneySupplyTrend);
        Assert.NotEmpty(economyInsights.Economy.BalanceDistribution);
        Assert.Contains(economyInsights.Economy.TopDonors, donor => donor.UserId == secondUser.Id);
        Assert.Contains(economyInsights.Economy.BiggestRobberies, robbery => robbery.UserId == firstUser.Id);
        Assert.Equal(168, economyInsights.Economy.EconomyHeatmap.Count);

        DashboardInsightsResponse stockInsights = await service.GetInsightsAsync(
            guildId: null,
            userId: null,
            channelId: null,
            days: 7,
            scope: "global",
            sortDirection: "desc",
            minActivity: 0,
            view: "stocks",
            startDateUtc: start,
            endDateUtc: today);

        Assert.Equal(3, stockInsights.Stocks.Stocks);
        Assert.Equal(1, stockInsights.Stocks.UserStocks);
        Assert.Equal(1, stockInsights.Stocks.ServerStocks);
        Assert.Equal(1, stockInsights.Stocks.ChannelStocks);
        Assert.Equal(100m, stockInsights.Stocks.BuyVolume);
        Assert.Equal(60m, stockInsights.Stocks.SellVolume);
        Assert.Equal(25m, stockInsights.Stocks.StockTransferVolume);
        Assert.NotEmpty(stockInsights.Stocks.MostValuableStocks);
        Assert.NotEmpty(stockInsights.Stocks.HoldingsByUser);
        Assert.NotEmpty(stockInsights.Stocks.HoldingsTable);
        Assert.NotEmpty(stockInsights.Stocks.TradeVolumeTimeline);
        Assert.Contains(stockInsights.Stocks.ActivityToPrice, point => point.StockId == userStock.Id && point.Xp > 0);
    }

    [Fact]
    public async Task GetGlobalOverviewAsync_UserViewUsesExplicitDateRangeForActivityHighlights()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User inWindowUser) = await SeedBaseAsync(testDb.Db);
        User outOfWindowUser = new()
        {
            DiscordId = 456,
            Username = "outside-window"
        };
        Channel inWindowChannel = new()
        {
            DiscordId = 1001,
            Name = "selected-window"
        };
        Channel outOfWindowChannel = new()
        {
            DiscordId = 1002,
            Name = "outside-window"
        };
        testDb.Db.Users.Add(outOfWindowUser);
        testDb.Db.Channels.AddRange(inWindowChannel, outOfWindowChannel);
        await testDb.Db.SaveChangesAsync();

        DateTime startDate = DateTime.UtcNow.Date.AddDays(-10);
        DateTime endDate = DateTime.UtcNow.Date.AddDays(-8);
        List<UserActivity> activities =
        [
            new()
            {
                GuildId = guild.Id,
                UserId = inWindowUser.Id,
                DiscordChannelId = inWindowChannel.DiscordId,
                DiscordMessageId = 1,
                XpGained = 10,
                MessageLength = 40,
                InsertDate = startDate.AddHours(1)
            },
            new()
            {
                GuildId = guild.Id,
                UserId = inWindowUser.Id,
                DiscordChannelId = inWindowChannel.DiscordId,
                DiscordMessageId = 2,
                XpGained = 15,
                MessageLength = 42,
                InsertDate = endDate.AddHours(1)
            }
        ];
        activities.AddRange(Enumerable.Range(0, 5).Select(index => new UserActivity
        {
            GuildId = guild.Id,
            UserId = outOfWindowUser.Id,
            DiscordChannelId = outOfWindowChannel.DiscordId,
            DiscordMessageId = (ulong)(10 + index),
            XpGained = 100,
            MessageLength = 80,
            InsertDate = DateTime.UtcNow.Date.AddDays(-2).AddMinutes(index)
        }));
        testDb.Db.UserActivity.AddRange(activities);
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardGlobalOverviewResponse overview = await service.GetGlobalOverviewAsync(
            days: 30,
            view: "users",
            startDateUtc: startDate,
            endDateUtc: endDate);

        DashboardGlobalUserActivity xpGainer = Assert.Single(overview.Highlights.BiggestXpGainers);
        DashboardGlobalUserActivity activeUser = Assert.Single(overview.Highlights.MostActiveUsers);
        DashboardGlobalChannelActivity activeChannel = Assert.Single(overview.Highlights.MostActiveChannels);
        Assert.Equal(inWindowUser.Id, xpGainer.UserId);
        Assert.Equal(25, xpGainer.Xp);
        Assert.Equal(inWindowUser.Id, activeUser.UserId);
        Assert.Equal(2, activeUser.Messages);
        Assert.Equal(inWindowChannel.DiscordId.ToString(), activeChannel.DiscordId);
        Assert.Equal(2, activeChannel.Messages);
    }

    [Fact]
    public async Task GetGuildOptionsAsync_ReturnsLightweightServerPickerRows()
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
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardGuildSummary option = Assert.Single(await service.GetGuildOptionsAsync());

        Assert.Equal(guild.Id, option.Id);
        Assert.Equal(guild.Name, option.Name);
        Assert.Equal(0, option.TrackedUsers);
        Assert.Equal(0, option.Messages);
        Assert.Equal(0, option.Xp);
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
    public async Task GetActivitySeriesAsync_UsesExplicitDateRange()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);
        DateTime startDate = DateTime.UtcNow.Date.AddDays(-10);
        DateTime endDate = DateTime.UtcNow.Date.AddDays(-5);

        testDb.Db.UserActivity.AddRange(
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = user.Id,
                DiscordChannelId = 1,
                DiscordMessageId = 1,
                XpGained = 10,
                MessageLength = 40,
                InsertDate = startDate.AddHours(2)
            },
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = user.Id,
                DiscordChannelId = 1,
                DiscordMessageId = 2,
                XpGained = 20,
                MessageLength = 60,
                InsertDate = endDate.AddHours(23)
            },
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = user.Id,
                DiscordChannelId = 1,
                DiscordMessageId = 3,
                XpGained = 30,
                MessageLength = 80,
                InsertDate = endDate.AddDays(1)
            });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardActivitySeriesResponse series = await service.GetActivitySeriesAsync(
            guild.Id,
            days: 365,
            startDateUtc: startDate,
            endDateUtc: endDate);

        Assert.Equal(6, series.Days);
        Assert.Equal(6, series.Points.Count);
        Assert.Equal(1, series.Points[0].Messages);
        Assert.Equal(1, series.Points[5].Messages);
        Assert.Equal(30, series.Points.Sum(point => point.Xp));
    }

    [Fact]
    public async Task GetActivitySeriesAsync_AllowsMultiYearExplicitDateRange()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);
        DateTime startDate = DateTime.UtcNow.Date.AddYears(-2);
        DateTime endDate = DateTime.UtcNow.Date;

        testDb.Db.UserActivity.AddRange(
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = user.Id,
                DiscordChannelId = 1,
                DiscordMessageId = 1,
                XpGained = 10,
                MessageLength = 40,
                InsertDate = startDate.AddHours(2)
            },
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = user.Id,
                DiscordChannelId = 1,
                DiscordMessageId = 2,
                XpGained = 20,
                MessageLength = 60,
                InsertDate = endDate.AddHours(2)
            });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardActivitySeriesResponse series = await service.GetActivitySeriesAsync(
            guild.Id,
            days: 1000,
            startDateUtc: startDate,
            endDateUtc: endDate);

        Assert.True(series.Days > 366);
        Assert.Equal(series.Days, series.Points.Count);
        Assert.Equal(1, series.Points[0].Messages);
        Assert.Equal(1, series.Points[^1].Messages);
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
        testDb.Db.Roles.Add(new Role
        {
            GuildId = guild.Id,
            RoleId = 777,
            RoleType = RoleType.TopOnePercent
        });
        testDb.Db.ReactionRoleMessages.Add(new ReactionRoleMessage
        {
            GuildId = guild.Id,
            ChannelId = channel.DiscordId,
            MessageId = 999,
            UseButtons = true,
            Items =
            [
                new ReactionRoleItem
                {
                    RoleId = 777,
                    Emoji = "star",
                    CustomId = "activity-role"
                }
            ]
        });
        testDb.Db.Logs.AddRange(
            new Log
            {
                Severity = 2,
                Message = $"warning for {guild.DiscordId} in {channel.DiscordId}",
                Version = "test",
                InsertDate = DateTime.UtcNow
            },
            new Log
            {
                Severity = 1,
                Message = $"error for {user.DiscordId}",
                Version = "test",
                InsertDate = DateTime.UtcNow
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
        Assert.Equal(75, insights.ButtonGame.HighestScoreEver);
        Assert.Single(insights.ButtonGame.TopGlobalScores);
        Assert.Single(insights.ButtonGame.TopIndividualScores);
        Assert.Equal(168, insights.ButtonGame.HourByWeekdayHeatmap.Count);
        Assert.NotEmpty(insights.ButtonGame.CalendarHeatmap);
        Assert.Equal(1, insights.Operations.Reminders.Pending);
        Assert.Equal(1, insights.Operations.Reminders.DueNext24Hours);
        Assert.NotEmpty(insights.Operations.Reminders.ByChannel);
        Assert.NotEmpty(insights.Operations.Reminders.CreationTrend);
        Assert.Equal(1, insights.Operations.Moderation.PendingTemporaryBans);
        Assert.Equal(1, insights.Operations.Moderation.ReactionRoleMessages);
        Assert.Equal(1, insights.Operations.Moderation.ReactionRoleItems);
        Assert.NotEmpty(insights.Operations.Moderation.TemporaryBanTimeline);
        Assert.NotEmpty(insights.Operations.Moderation.BanStatus);
        Assert.NotEmpty(insights.Operations.Moderation.ReactionRoleTypes);
        Assert.NotEmpty(insights.Operations.Moderation.ActivityRoleDistribution);
        Assert.NotEmpty(insights.Operations.Moderation.ServerScorecards);
        Assert.Equal(1, insights.Operations.Logs.Total);
        Assert.Equal(1, insights.Operations.Logs.Warnings);
        Assert.Equal(0, insights.Operations.Logs.Errors);
        Assert.NotEmpty(insights.Operations.Logs.LogsByVersion);
        Assert.NotEmpty(insights.Operations.Logs.CommonMessages);
        Assert.NotEmpty(insights.Operations.Logs.RecentIncidents);
    }

    [Fact]
    public async Task GetInsightsAsync_ServerSummaryReturnsServerDashboardInsights()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User firstUser) = await SeedBaseAsync(testDb.Db);
        guild.Prefix = "$";
        guild.WelcomeMessages = true;
        guild.WelcomeChannelId = 555;
        guild.PinsChannelId = 556;
        guild.LevelUpMessages = true;
        guild.LevelUpMessagesChannelId = 555;
        guild.LevelUpQuotes = true;
        guild.LevelUpQuotesChannelId = 556;
        guild.QuotesApprovalChannelId = 556;
        guild.UseActivityRoles = true;

        User secondUser = new()
        {
            DiscordId = 456,
            Username = "rising",
            Balance = 250m
        };
        Channel general = new()
        {
            DiscordId = 555,
            Name = "general"
        };
        Channel quotes = new()
        {
            DiscordId = 556,
            Name = "quotes"
        };
        testDb.Db.Users.Add(secondUser);
        testDb.Db.Channels.AddRange(general, quotes);
        await testDb.Db.SaveChangesAsync();

        DateTime startDate = DateTime.UtcNow.Date.AddDays(-6);
        testDb.Db.UserLevels.AddRange(
            new UserLevels
            {
                GuildId = guild.Id,
                UserId = firstUser.Id,
                TotalXp = 200,
                UserMessageCount = 4
            },
            new UserLevels
            {
                GuildId = guild.Id,
                UserId = secondUser.Id,
                TotalXp = 300,
                UserMessageCount = 5
            });
        testDb.Db.UserActivity.AddRange(
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = firstUser.Id,
                DiscordChannelId = general.DiscordId,
                DiscordMessageId = 1,
                XpGained = 20,
                MessageLength = 32,
                InsertDate = startDate.AddHours(1)
            },
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = secondUser.Id,
                DiscordChannelId = general.DiscordId,
                DiscordMessageId = 2,
                XpGained = 30,
                MessageLength = 80,
                InsertDate = startDate.AddDays(4).AddHours(1)
            },
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = secondUser.Id,
                DiscordChannelId = quotes.DiscordId,
                DiscordMessageId = 3,
                XpGained = 40,
                MessageLength = 90,
                InsertDate = startDate.AddDays(5).AddHours(1)
            });
        testDb.Db.Roles.Add(new Role
        {
            GuildId = guild.Id,
            RoleId = 999,
            RoleType = RoleType.TopOnePercent
        });
        testDb.Db.Reminders.Add(new Reminder
        {
            GuildId = guild.Id,
            UserId = firstUser.Id,
            ChannelId = general.DiscordId,
            Text = "server dashboard reminder",
            DueDate = DateTime.UtcNow.AddHours(3)
        });
        testDb.Db.ButtonGamePresses.Add(new ButtonGamePress
        {
            GuildId = guild.Id,
            UserId = firstUser.Id,
            Score = 10
        });
        Quote approvedQuote = new()
        {
            GuildId = guild.Id,
            UserId = firstUser.Id,
            Content = "approved server quote",
            Approved = true
        };
        Quote pendingQuote = new()
        {
            GuildId = guild.Id,
            UserId = secondUser.Id,
            Content = "pending server quote",
            Approved = false
        };
        testDb.Db.Quotes.AddRange(approvedQuote, pendingQuote);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.QuoteApprovalMessages.Add(new QuoteApprovalMessage
        {
            QuoteId = pendingQuote.Id,
            ApprovalMessageId = 1234,
            Approved = false,
            Type = QuoteApprovalType.AddRequest
        });
        testDb.Db.StockTransactions.Add(new StockTransaction
        {
            UserId = firstUser.Id,
            Type = TransactionType.Transfer,
            Amount = 25m,
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
            minActivity: 1,
            view: "summary",
            startDateUtc: startDate,
            endDateUtc: DateTime.UtcNow.Date);

        DashboardServerInsights server = Assert.IsType<DashboardServerInsights>(insights.Server);
        Assert.Equal(guild.Id, server.Identity.GuildId);
        Assert.Equal("$", server.Configuration.Prefix);
        Assert.Equal("general", server.Configuration.WelcomeChannel.Name);
        Assert.Equal("quotes", server.Configuration.QuoteApprovalChannel.Name);
        Assert.True(server.Configuration.ActivityRoles);
        Assert.Equal(2, server.Totals.KnownUsers);
        Assert.Equal(3, server.Totals.TrackedMessages);
        Assert.Equal(500, server.Totals.TotalXp);
        Assert.Equal(2, server.Totals.TotalQuotes);
        Assert.Equal(1, server.Totals.PendingQuoteApprovals);
        Assert.Equal(1, server.Totals.ActiveReminders);
        Assert.Equal(1, server.Totals.ButtonPresses);
        Assert.NotEmpty(server.TopUsersByAverageMessageLength);
        Assert.NotEmpty(server.FastestRisingUsers);
        Assert.NotEmpty(server.ChannelHeatmap);
        Assert.Contains(server.ConfigurationChecklist, item => item.Label == "Quote approval channel" && item.Passed);
        Assert.InRange(server.Health.Score, 0, 100);
    }

    [Fact]
    public async Task GetInsightsAsync_ViewUsersReturnsOnlyUserTablesAndFilterOptions()
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
            TotalXp = 100,
            UserMessageCount = 2
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
            Content = "should stay out of users view",
            Approved = true
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
            minActivity: 1,
            view: "users");

        Assert.Equal(0, insights.Activity.Messages);
        Assert.Equal("general", Assert.Single(insights.Channels).Name);
        Assert.Equal(user.Username, Assert.Single(insights.Users).Username);
        Assert.Empty(insights.Quotes.Statuses);
        Assert.Empty(insights.Economy.DailyFlow);
        Assert.Empty(insights.Operations.RecentLogs);
        Assert.Contains(insights.FilterOptions.Users, option => option.UserId == user.Id);
        Assert.Contains(insights.FilterOptions.Channels, option => option.DiscordId == channel.DiscordId.ToString());
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
    public async Task GetInsightsAsync_UserScopeWithoutSelectedUserFallsBackToGlobalScope()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (_, User user) = await SeedBaseAsync(testDb.Db);

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardInsightsResponse insights = await service.GetInsightsAsync(
            guildId: null,
            userId: null,
            channelId: null,
            days: 7,
            scope: "user",
            sortDirection: "desc",
            minActivity: 1,
            view: "users");

        Assert.Equal("global", insights.Scope);
        Assert.Null(insights.UserId);
        Assert.Contains(insights.FilterOptions.Users, option => option.UserId == user.Id);
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

    [Fact]
    public async Task GetInsightsAsync_UserSummaryReturnsCompleteUserProfile()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (Guild guild, User user) = await SeedBaseAsync(testDb.Db);
        User voter = new()
        {
            DiscordId = 777,
            Username = "voter"
        };
        Channel channel = new()
        {
            DiscordId = 555,
            Name = "general"
        };
        testDb.Db.Users.Add(voter);
        testDb.Db.Channels.Add(channel);
        await testDb.Db.SaveChangesAsync();

        DateTime startDate = DateTime.UtcNow.Date.AddDays(-4);
        testDb.Db.UserLevels.Add(new UserLevels
        {
            GuildId = guild.Id,
            UserId = user.Id,
            TotalXp = 400,
            UserMessageCount = 3,
            UserAverageMessageLength = 50,
            UserAverageMessageLengthEma = 55
        });
        testDb.Db.UserActivity.AddRange(
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = user.Id,
                DiscordChannelId = channel.DiscordId,
                DiscordMessageId = 1,
                XpGained = 20,
                MessageLength = 40,
                InsertDate = startDate.AddDays(1)
            },
            new UserActivity
            {
                GuildId = guild.Id,
                UserId = user.Id,
                DiscordChannelId = channel.DiscordId,
                DiscordMessageId = 2,
                XpGained = 30,
                MessageLength = 80,
                InsertDate = startDate.AddDays(2)
            });
        Quote quote = new()
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = "profile quote",
            Approved = true,
            InsertDate = startDate.AddDays(1)
        };
        Quote votedQuote = new()
        {
            GuildId = guild.Id,
            UserId = voter.Id,
            Content = "voted quote",
            Approved = true,
            InsertDate = startDate.AddDays(1)
        };
        testDb.Db.Quotes.AddRange(quote, votedQuote);
        testDb.Db.ButtonGamePresses.Add(new ButtonGamePress
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Score = 75,
            InsertDate = startDate.AddDays(2)
        });
        testDb.Db.Reminders.Add(new Reminder
        {
            GuildId = guild.Id,
            UserId = user.Id,
            ChannelId = channel.DiscordId,
            Text = "profile reminder",
            DueDate = DateTime.UtcNow.AddHours(3)
        });
        await testDb.Db.SaveChangesAsync();

        testDb.Db.QuoteScores.AddRange(
            new QuoteScore { QuoteId = quote.Id, UserId = voter.Id, Score = 6, InsertDate = startDate.AddDays(2) },
            new QuoteScore { QuoteId = votedQuote.Id, UserId = user.Id, Score = 1, InsertDate = startDate.AddDays(2) });
        Stock stock = new()
        {
            EntityType = StockEntityType.User,
            EntityId = voter.Id,
            Price = 20m,
            DailyChangePercent = 5m
        };
        testDb.Db.Stocks.Add(stock);
        await testDb.Db.SaveChangesAsync();

        testDb.Db.StockHoldings.Add(new StockHolding
        {
            UserId = user.Id,
            StockId = stock.Id,
            Shares = 3m,
            TotalInvested = 45m
        });
        testDb.Db.StockTransactions.AddRange(
            new StockTransaction
            {
                UserId = user.Id,
                StockId = stock.Id,
                Type = TransactionType.StockBuy,
                Amount = 45m,
                Fee = 1m,
                InsertDate = startDate.AddDays(2)
            },
            new StockTransaction
            {
                UserId = user.Id,
                Type = TransactionType.SlotsWin,
                Amount = 25m,
                InsertDate = startDate.AddDays(3)
            });
        await testDb.Db.SaveChangesAsync();

        DashboardStatsService service = CreateService(testDb.Db);

        DashboardInsightsResponse insights = await service.GetInsightsAsync(
            guild.Id,
            user.Id,
            channelId: null,
            days: 5,
            scope: "user",
            sortDirection: "desc",
            minActivity: 1,
            view: "summary",
            startDateUtc: startDate,
            endDateUtc: DateTime.UtcNow.Date);

        DashboardUserProfileInsights profile = Assert.IsType<DashboardUserProfileInsights>(insights.UserProfile);
        Assert.Equal(user.Id, profile.Identity.UserId);
        Assert.Equal(400, profile.Totals.TotalXp);
        Assert.Equal(3, profile.Totals.TotalMessages);
        Assert.Equal(2, profile.Activity.Messages);
        Assert.Single(profile.ServerLevels);
        Assert.Single(profile.ServerContribution);
        Assert.Equal("general", Assert.Single(profile.ChannelContribution).Label);
        Assert.Equal(6, profile.QuotePerformance.ScoreReceived);
        Assert.Equal(1, profile.QuotePerformance.VotesGiven);
        Assert.Equal(60m, profile.EconomyPerformance.PortfolioValue);
        Assert.Equal(15m, profile.EconomyPerformance.UnrealizedGains);
        Assert.Equal(1, profile.EconomyPerformance.Trades);
        Assert.Single(profile.StockHoldings);
        Assert.Equal(75, profile.ButtonGame.Score);
        Assert.Single(profile.Reminders);
        Assert.Equal(5, profile.MessageLengthTrend.Count);
        Assert.Equal(5, profile.LevelProgression.Count);
        Assert.Equal(168, profile.HourByWeekdayHeatmap.Count);
    }

    private static DashboardStatsService CreateService(DB db) =>
        new(db, new DashboardApiOptions(
            "http://127.0.0.1:5267",
            ["http://localhost:3000"],
            string.Empty,
            DashboardApiOptions.DefaultMaxActivityDays));

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
