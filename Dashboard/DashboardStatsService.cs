using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;
using Morpheus.Services;
using Morpheus.Utilities;

namespace Morpheus.Dashboard;

public sealed class DashboardStatsService(DB dbContext, DashboardApiOptions options)
{
    private const int DefaultLeaderboardLimit = 10;
    private const int MaxLeaderboardLimit = 50;
    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";
    private const string UbiPoolSettingKey = "ubi_pool";
    private const string SlotsVaultSettingKey = "slots_vault";
    private const decimal SlotsVaultDefaultAmount = 10000.00m;

    public async Task<DashboardOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;
        DateTime last24Hours = now.AddDays(-1);
        DateTime last30Days = now.AddDays(-30);

        int guildCount = await dbContext.Guilds.AsNoTracking().CountAsync(cancellationToken);
        int userCount = await dbContext.Users.AsNoTracking().CountAsync(cancellationToken);
        int stockCount = await dbContext.Stocks.AsNoTracking().CountAsync(cancellationToken);

        long totalMessages = await dbContext.UserLevels
            .AsNoTracking()
            .SumAsync(levels => (long?)levels.UserMessageCount, cancellationToken) ?? 0L;
        long totalXp = await dbContext.UserLevels
            .AsNoTracking()
            .SumAsync(levels => (long?)levels.TotalXp, cancellationToken) ?? 0L;

        IQueryable<UserActivity> recentActivity = dbContext.UserActivity
            .AsNoTracking()
            .Where(activity => activity.InsertDate >= last30Days);

        int activeUsersLast30Days = await recentActivity
            .Select(activity => activity.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
        long messagesLast30Days = await recentActivity.LongCountAsync(cancellationToken);
        long xpLast30Days = await recentActivity
            .SumAsync(activity => (long?)activity.XpGained, cancellationToken) ?? 0L;
        DateTime? lastActivityAtUtc = await dbContext.UserActivity
            .AsNoTracking()
            .Select(activity => (DateTime?)activity.InsertDate)
            .MaxAsync(cancellationToken);

        int approvedQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote => quote.Approved && !quote.Removed, cancellationToken);
        int pendingQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote => !quote.Approved && !quote.Removed, cancellationToken);
        int removedQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote => quote.Removed, cancellationToken);
        int totalQuoteScore = await dbContext.QuoteScores
            .AsNoTracking()
            .SumAsync(score => (int?)score.Score, cancellationToken) ?? 0;

        decimal totalBalance = await GetTotalBalanceAsync(cancellationToken);
        decimal stockPortfolioValue = await GetStockPortfolioValueAsync(cancellationToken);

        long totalLogs = await dbContext.Logs.AsNoTracking().LongCountAsync(cancellationToken);
        int logsLast24Hours = await dbContext.Logs
            .AsNoTracking()
            .CountAsync(log => log.InsertDate >= last24Hours, cancellationToken);
        DateTime? lastLogAtUtc = await dbContext.Logs
            .AsNoTracking()
            .Select(log => (DateTime?)log.InsertDate)
            .MaxAsync(cancellationToken);

        return new DashboardOverviewResponse(
            now,
            Env.StartTime,
            (long)(now - Env.StartTime).TotalSeconds,
            new DashboardSystemStats(guildCount, userCount, stockCount),
            new DashboardActivityStats(
                totalMessages,
                totalXp,
                activeUsersLast30Days,
                messagesLast30Days,
                xpLast30Days,
                lastActivityAtUtc),
            new DashboardQuoteStats(approvedQuotes, pendingQuotes, removedQuotes, totalQuoteScore),
            new DashboardEconomyStats(totalBalance, stockPortfolioValue),
            new DashboardLogStats(totalLogs, logsLast24Hours, lastLogAtUtc));
    }

    public async Task<DashboardGlobalOverviewResponse> GetGlobalOverviewAsync(
        int days,
        string? view = null,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        CancellationToken cancellationToken = default)
    {
        DashboardDateRange dateRange = ResolveDateRange(days, startDateUtc, endDateUtc);
        int safeDays = dateRange.Days;
        string safeView = NormalizeGlobalOverviewView(view);
        DateTime now = DateTime.UtcNow;
        DateTime startDate = dateRange.StartDateUtc;
        DateTime endExclusiveDate = dateRange.EndExclusiveUtc;
        DateTime today = now.Date;
        DateTime last24Hours = now.AddDays(-1);
        DateTime last7Days = now.AddDays(-7);
        DateTime last30Days = now.AddDays(-30);

        bool includeAll = safeView == "all";

        DashboardGlobalTotals totals = safeView == "summary" || includeAll
            ? await BuildGlobalTotalsAsync(last24Hours, dateRange.EndDateUtc, endExclusiveDate, cancellationToken)
            : EmptyGlobalTotals();

        IReadOnlyList<DashboardGlobalServerActivity> mostActiveServersToday = [];
        IReadOnlyList<DashboardGlobalServerActivity> mostActiveServersThisWeek = [];
        IReadOnlyList<DashboardGlobalServerActivity> mostActiveServersThisMonth = [];
        IReadOnlyList<DashboardGlobalServerActivity> mostActiveServersAllTime = [];
        IReadOnlyList<DashboardGlobalServerActivity> mostActiveServersSelectedWindow = [];
        IReadOnlyList<DashboardGlobalUserActivity> biggestXpGainers = [];
        IReadOnlyList<DashboardGlobalWealthUser> richestUsersByBalance = [];
        IReadOnlyList<DashboardGlobalWealthUser> richestUsersByNetWorth = [];
        IReadOnlyList<DashboardStockMover> biggestStockGainers = [];
        IReadOnlyList<DashboardStockMover> biggestStockLosers = [];
        IReadOnlyList<DashboardPopularQuote> mostPopularQuotes = [];
        IReadOnlyList<DashboardGlobalChannelActivity> mostActiveChannels = [];
        IReadOnlyList<DashboardGlobalUserActivity> mostActiveUsers = [];
        IReadOnlyList<DashboardRecentEntity> recentlyCreatedUsers = [];
        IReadOnlyList<DashboardRecentEntity> recentlyCreatedServers = [];
        IReadOnlyList<DashboardRecentQuote> recentlyCreatedQuotes = [];
        IReadOnlyList<DashboardRecentStock> recentlyCreatedStocks = [];
        IReadOnlyList<DashboardActivityDerivedPoint> activityPoints = [];
        IReadOnlyList<DashboardStackedServerActivityPoint> stackedServerActivity = [];
        IReadOnlyList<DashboardCalendarActivityCell> calendarActivity = [];
        IReadOnlyList<DashboardHeatmapCell> hourByWeekdayActivity = [];
        IReadOnlyList<DashboardCategoryValue> transactionTypes = [];
        IReadOnlyList<DashboardEconomyEventItem> recentEconomyEvents = [];
        IReadOnlyList<DashboardLogItem> recentBotHealthEvents = [];

        if (safeView == "servers" || includeAll)
        {
            mostActiveServersToday = await GetGlobalServerActivityAsync(today, 5, cancellationToken);
            mostActiveServersThisWeek = await GetGlobalServerActivityAsync(last7Days, 5, cancellationToken);
            mostActiveServersThisMonth = await GetGlobalServerActivityAsync(last30Days, 5, cancellationToken);
            mostActiveServersAllTime = await GetGlobalServerActivityAsync(null, 8, cancellationToken);
            mostActiveServersSelectedWindow = await GetGlobalServerActivityAsync(
                startDate,
                8,
                cancellationToken,
                endExclusiveDate);
        }

        if (safeView == "users" || includeAll)
        {
            biggestXpGainers = await GetGlobalUserActivityAsync(startDate, "xp", 10, cancellationToken, endExclusiveDate);
            richestUsersByBalance = await GetGlobalWealthUsersAsync(orderByNetWorth: false, 10, cancellationToken);
            richestUsersByNetWorth = await GetGlobalWealthUsersAsync(orderByNetWorth: true, 10, cancellationToken);
            mostActiveChannels = await GetGlobalChannelActivityAsync(startDate, 10, cancellationToken, endExclusiveDate);
            mostActiveUsers = await GetGlobalUserActivityAsync(startDate, "messages", 10, cancellationToken, endExclusiveDate);
            recentlyCreatedUsers = await GetRecentUsersAsync(8, cancellationToken);
            recentlyCreatedServers = await GetRecentServersAsync(8, cancellationToken);
            recentlyCreatedQuotes = await GetRecentQuotesAsync(8, cancellationToken);
            recentlyCreatedStocks = await GetRecentStocksAsync(8, cancellationToken);
        }

        if (safeView == "quotes" || includeAll)
        {
            mostPopularQuotes = await GetPopularQuotesAsync(10, cancellationToken);
        }

        if (safeView == "stocks" || includeAll)
        {
            DashboardStockMarketInsights stockMarket = await BuildStockMarketInsightsAsync(
                null,
                null,
                null,
                [],
                BuildActivityQuery(startDate, null, null, null, endExclusiveDate),
                cancellationToken);
            biggestStockGainers = stockMarket.Winners;
            biggestStockLosers = stockMarket.Losers;
        }

        if (safeView == "activity" || includeAll)
        {
            IQueryable<UserActivity> activityQuery = BuildActivityQuery(startDate, null, null, null, endExclusiveDate);
            DashboardActivityInsights activity = await BuildActivityInsightsAsync(
                activityQuery,
                startDate,
                safeDays,
                cancellationToken);
            activityPoints = activity.Points;
            stackedServerActivity = await BuildStackedServerActivityAsync(startDate, endExclusiveDate, safeDays, 6, cancellationToken);
            calendarActivity = BuildCalendarActivity(activity.Points);
            hourByWeekdayActivity = await BuildHeatmapAsync(activityQuery, cancellationToken);
        }

        if (safeView == "economy" || includeAll)
        {
            transactionTypes = await GetTransactionTypesAsync(startDate, endExclusiveDate, cancellationToken);
            recentEconomyEvents = await GetRecentEconomyEventsAsync(10, cancellationToken);
        }

        if (safeView == "operations" || includeAll)
        {
            recentBotHealthEvents = await GetRecentBotHealthEventsAsync(10, cancellationToken);
        }

        return new DashboardGlobalOverviewResponse(
            now,
            safeDays,
            totals,
            new DashboardGlobalHighlights(
                mostActiveServersToday,
                mostActiveServersThisWeek,
                mostActiveServersThisMonth,
                mostActiveServersAllTime,
                mostActiveServersSelectedWindow,
                biggestXpGainers,
                richestUsersByBalance,
                richestUsersByNetWorth,
                biggestStockGainers,
                biggestStockLosers,
                mostPopularQuotes,
                mostActiveChannels,
                mostActiveUsers,
                recentlyCreatedUsers,
                recentlyCreatedServers,
                recentlyCreatedQuotes,
                recentlyCreatedStocks),
            new DashboardGlobalVisuals(
                activityPoints,
                stackedServerActivity,
                calendarActivity,
                hourByWeekdayActivity,
                transactionTypes),
            new DashboardGlobalFeeds(
                recentEconomyEvents,
                recentBotHealthEvents));
    }

    private async Task<DashboardGlobalTotals> BuildGlobalTotalsAsync(
        DateTime last24Hours,
        DateTime latestDayStart,
        DateTime latestDayEndExclusive,
        CancellationToken cancellationToken)
    {
        int totalServers = await dbContext.Guilds.AsNoTracking().CountAsync(cancellationToken);
        int totalUsers = await dbContext.Users.AsNoTracking().CountAsync(cancellationToken);
        long totalMessages = await dbContext.UserLevels
            .AsNoTracking()
            .SumAsync(levels => (long?)levels.UserMessageCount, cancellationToken) ?? 0L;
        long totalXp = await dbContext.UserLevels
            .AsNoTracking()
            .SumAsync(levels => (long?)levels.TotalXp, cancellationToken) ?? 0L;
        IQueryable<UserActivity> latestDayActivity = dbContext.UserActivity
            .AsNoTracking()
            .Where(activity => activity.InsertDate >= latestDayStart && activity.InsertDate < latestDayEndExclusive);
        long latestDayMessages = await latestDayActivity.LongCountAsync(cancellationToken);
        long latestDayXp = await latestDayActivity
            .SumAsync(activity => (long?)activity.XpGained, cancellationToken) ?? 0L;
        int totalQuotes = await dbContext.Quotes.AsNoTracking().CountAsync(cancellationToken);
        int approvedQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote => quote.Approved && !quote.Removed, cancellationToken);
        int pendingQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote => !quote.Approved && !quote.Removed, cancellationToken);
        int pendingQuoteApprovals = await dbContext.QuoteApprovalMessages
            .AsNoTracking()
            .CountAsync(approval => !approval.Approved, cancellationToken);
        decimal totalBalance = await GetTotalBalanceAsync(cancellationToken);
        decimal portfolioValue = await GetStockPortfolioValueAsync(cancellationToken);
        decimal ubiPoolSize = await GetMoneySettingAmountAsync(UbiPoolSettingKey, 0m, cancellationToken);
        decimal slotsVaultSize = await GetMoneySettingAmountAsync(SlotsVaultSettingKey, SlotsVaultDefaultAmount, cancellationToken);
        long totalTransactions = await dbContext.StockTransactions.AsNoTracking().LongCountAsync(cancellationToken);
        long totalButtonPresses = await dbContext.ButtonGamePresses.AsNoTracking().LongCountAsync(cancellationToken);
        int activeReminders = await dbContext.Reminders.AsNoTracking().CountAsync(cancellationToken);
        int recentWarningsOrErrors = await dbContext.Logs
            .AsNoTracking()
            .CountAsync(log => log.InsertDate >= last24Hours && log.Severity <= 2, cancellationToken);

        return new DashboardGlobalTotals(
            totalServers,
            totalUsers,
            totalMessages,
            totalXp,
            latestDayMessages,
            latestDayXp,
            totalQuotes,
            approvedQuotes,
            pendingQuotes,
            pendingQuoteApprovals,
            totalBalance,
            totalBalance + portfolioValue,
            ubiPoolSize,
            slotsVaultSize,
            totalTransactions,
            totalButtonPresses,
            activeReminders,
            recentWarningsOrErrors);
    }

    private static DashboardGlobalTotals EmptyGlobalTotals()
    {
        return new DashboardGlobalTotals(
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0m,
            0m,
            0m,
            0m,
            0,
            0,
            0,
            0);
    }

    private static string NormalizeGlobalOverviewView(string? view)
    {
        if (string.IsNullOrWhiteSpace(view))
            return "all";

        return view is "activity" or "servers" or "users" or "quotes" or "economy" or "stocks" or "operations"
            ? view
            : "summary";
    }

    private static string NormalizeDashboardInsightView(string? view)
    {
        if (string.IsNullOrWhiteSpace(view))
            return "all";

        return view is "summary" or "activity" or "users" or "quotes" or "economy" or "stocks" or "operations" or "settings"
            ? view
            : "summary";
    }

    public async Task<IReadOnlyList<DashboardGuildSummary>> GetGuildsAsync(CancellationToken cancellationToken = default)
    {
        var guilds = await dbContext.Guilds
            .AsNoTracking()
            .OrderBy(guild => guild.Name)
            .Select(guild => new
            {
                guild.Id,
                guild.DiscordId,
                guild.Name,
                guild.InsertDate,
                TrackedUsers = dbContext.UserLevels.Count(levels => levels.GuildId == guild.Id),
                Messages = dbContext.UserActivity.LongCount(activity => activity.GuildId == guild.Id),
                Xp = dbContext.UserActivity
                    .Where(activity => activity.GuildId == guild.Id)
                    .Sum(activity => (long?)activity.XpGained) ?? 0L,
                ApprovedQuotes = dbContext.Quotes.Count(quote =>
                    quote.GuildId == guild.Id &&
                    quote.Approved &&
                    !quote.Removed)
            })
            .ToListAsync(cancellationToken);

        return [.. guilds.Select(guild => new DashboardGuildSummary(
            guild.Id,
            guild.DiscordId.ToString(),
            guild.Name,
            guild.InsertDate,
            guild.TrackedUsers,
            guild.Messages,
            guild.Xp,
            guild.ApprovedQuotes))];
    }

    public async Task<IReadOnlyList<DashboardGuildSummary>> GetGuildOptionsAsync(CancellationToken cancellationToken = default)
    {
        var guilds = await dbContext.Guilds
            .AsNoTracking()
            .OrderBy(guild => guild.Name)
            .Select(guild => new
            {
                guild.Id,
                guild.DiscordId,
                guild.Name,
                guild.InsertDate
            })
            .ToListAsync(cancellationToken);

        return [.. guilds.Select(guild => new DashboardGuildSummary(
            guild.Id,
            guild.DiscordId.ToString(),
            guild.Name,
            guild.InsertDate,
            0,
            0,
            0,
            0))];
    }

    public async Task<DashboardActivitySeriesResponse> GetActivitySeriesAsync(
        int? guildId,
        int days,
        int? userId = null,
        string? channelId = null,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        CancellationToken cancellationToken = default)
    {
        DashboardDateRange dateRange = ResolveDateRange(days, startDateUtc, endDateUtc);
        int safeDays = dateRange.Days;
        DateTime startDate = dateRange.StartDateUtc;
        DateTime endExclusiveDate = dateRange.EndExclusiveUtc;
        ulong? channelDiscordId = ParseDiscordId(channelId);

        IQueryable<UserActivity> query = BuildActivityQuery(startDate, guildId, userId, channelDiscordId, endExclusiveDate);

        var rows = await query
            .GroupBy(activity => activity.InsertDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                ActiveUsers = group.Select(activity => activity.UserId).Distinct().Count(),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0
            })
            .OrderBy(row => row.Date)
            .ToListAsync(cancellationToken);

        Dictionary<DateTime, DashboardActivityPoint> pointsByDate = rows.ToDictionary(
            row => row.Date.Date,
            row => new DashboardActivityPoint(
                DateTime.SpecifyKind(row.Date.Date, DateTimeKind.Utc),
                row.Messages,
                row.Xp,
                row.ActiveUsers,
                Math.Round(row.AverageMessageLength, 1)));

        List<DashboardActivityPoint> points = [];
        for (int offset = 0; offset < safeDays; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            points.Add(pointsByDate.TryGetValue(date, out DashboardActivityPoint? point)
                ? point
                : new DashboardActivityPoint(DateTime.SpecifyKind(date, DateTimeKind.Utc), 0, 0, 0, 0.0));
        }

        return new DashboardActivitySeriesResponse(guildId, safeDays, points);
    }

    public async Task<DashboardLeaderboardResponse> GetActivityLeaderboardAsync(
        int? guildId,
        string metric,
        int? days,
        int limit,
        int? userId = null,
        string? channelId = null,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedMetric = NormalizeMetric(metric);
        int safeLimit = ClampLimit(limit);
        DashboardDateRange? dateRange = days is > 0 || startDateUtc.HasValue || endDateUtc.HasValue
            ? ResolveDateRange(days ?? options.MaxActivityDays, startDateUtc, endDateUtc)
            : null;
        int? safeDays = dateRange?.Days;
        ulong? channelDiscordId = ParseDiscordId(channelId);

        List<DashboardLeaderboardRow> rows = dateRange is not null
            ? await GetActivityLeaderboardRowsAsync(
                guildId,
                userId,
                channelDiscordId,
                normalizedMetric,
                dateRange.StartDateUtc,
                safeLimit,
                cancellationToken,
                dateRange.EndExclusiveUtc)
            : channelDiscordId.HasValue
                ? await GetActivityLeaderboardRowsAsync(guildId, userId, channelDiscordId, normalizedMetric, null, safeLimit, cancellationToken)
                : await GetAllTimeLeaderboardRowsAsync(guildId, userId, normalizedMetric, safeLimit, cancellationToken);

        Dictionary<int, (string DiscordId, string Username)> users = await GetUserLabelsAsync(
            rows.Select(row => row.UserId),
            cancellationToken);

        List<DashboardLeaderboardItem> items =
        [
            .. rows.Select((row, index) =>
            {
                (string discordId, string username) = users.GetValueOrDefault(row.UserId, (string.Empty, "Unknown"));
                int? level = normalizedMetric == "xp"
                    ? ActivityLevelService.CalculateLevel(row.Value)
                    : null;

                return new DashboardLeaderboardItem(
                    index + 1,
                    row.UserId,
                    discordId,
                    username,
                    row.Value,
                    level,
                    row.LastActivityAtUtc);
            })
        ];

        return new DashboardLeaderboardResponse(guildId, normalizedMetric, safeDays, safeLimit, items);
    }

    public async Task<DashboardInsightsResponse> GetInsightsAsync(
        int? guildId,
        int? userId,
        string? channelId,
        int days,
        string? scope,
        string? sortDirection,
        int? minActivity,
        string? view = null,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        CancellationToken cancellationToken = default)
    {
        DashboardDateRange dateRange = ResolveDateRange(days, startDateUtc, endDateUtc);
        int safeDays = dateRange.Days;
        int safeMinActivity = Math.Max(0, minActivity ?? 1);
        string normalizedSortDirection = NormalizeSortDirection(sortDirection);
        string safeView = NormalizeDashboardInsightView(view);
        bool includeAll = safeView == "all";
        bool includeActivity = includeAll || safeView is "summary" or "activity";
        bool includeUserTables = includeAll || safeView == "users";
        bool includeQuotes = includeAll || safeView == "quotes";
        bool includeEconomy = includeAll || safeView == "economy";
        bool includeStocks = includeAll || safeView == "stocks";
        bool includeOperations = includeAll || safeView == "operations";
        bool includeSettings = includeAll || safeView == "settings";
        DashboardScopeFilters filters = NormalizeScopeFilters(scope, guildId, userId, channelId);
        int? effectiveGuildId = filters.GuildId;
        int? effectiveUserId = filters.UserId;
        ulong? channelDiscordId = filters.ChannelDiscordId;
        DateTime startDate = dateRange.StartDateUtc;
        DateTime endExclusiveDate = dateRange.EndExclusiveUtc;

        IQueryable<UserActivity> activityQuery = BuildActivityQuery(
            startDate,
            effectiveGuildId,
            effectiveUserId,
            channelDiscordId,
            endExclusiveDate);

        IReadOnlyList<ScopedUserRow> scopedUsers = includeEconomy || includeStocks
            ? await GetScopedUsersAsync(effectiveGuildId, effectiveUserId, cancellationToken)
            : [];
        List<int> scopedUserIds = includeStocks
            ? [.. scopedUsers.Select(user => user.UserId)]
            : [];

        DashboardActivityInsights activity = includeActivity
            ? await BuildActivityInsightsAsync(
                activityQuery,
                startDate,
                safeDays,
                cancellationToken)
            : EmptyActivityInsights();
        IReadOnlyList<DashboardChannelActivity> channels = includeUserTables
            ? await BuildChannelActivityAsync(
                activityQuery,
                normalizedSortDirection,
                safeMinActivity,
                cancellationToken)
            : [];

        IReadOnlyList<DashboardUserActivitySummary> users = includeUserTables
            ? await BuildUserActivitySummariesAsync(
                activityQuery,
                effectiveGuildId,
                startDate,
                endExclusiveDate,
                normalizedSortDirection,
                safeMinActivity,
                cancellationToken)
            : [];

        IReadOnlyList<DashboardHeatmapCell> heatmap = safeView == "activity" || includeAll
            ? await BuildHeatmapAsync(
                activityQuery,
                cancellationToken)
            : [];
        DashboardQuoteInsights quotes = includeQuotes
            ? await BuildQuoteInsightsAsync(
                effectiveGuildId,
                effectiveUserId,
                startDate,
                endExclusiveDate,
                cancellationToken)
            : EmptyQuoteInsights();
        DashboardEconomyInsights economy = includeEconomy
            ? await BuildEconomyInsightsAsync(
                scopedUsers,
                effectiveGuildId,
                effectiveUserId,
                startDate,
                endExclusiveDate,
                safeDays,
                cancellationToken)
            : EmptyEconomyInsights();
        DashboardStockMarketInsights stocks = includeStocks
            ? await BuildStockMarketInsightsAsync(
                effectiveGuildId,
                effectiveUserId,
                channelDiscordId,
                scopedUserIds,
                activityQuery,
                cancellationToken)
            : EmptyStockMarketInsights();
        DashboardButtonGameInsights buttonGame = includeOperations
            ? await BuildButtonGameInsightsAsync(
                effectiveGuildId,
                effectiveUserId,
                startDate,
                endExclusiveDate,
                safeDays,
                cancellationToken)
            : EmptyButtonGameInsights();
        DashboardOperationsInsights operations = includeOperations
            ? await BuildOperationsInsightsAsync(
                effectiveGuildId,
                effectiveUserId,
                channelDiscordId,
                startDate,
                endExclusiveDate,
                safeDays,
                cancellationToken)
            : EmptyOperationsInsights();
        IReadOnlyList<DashboardGuildSettingsSummary> settings = includeSettings
            ? await BuildSettingsInsightsAsync(
                effectiveGuildId,
                cancellationToken)
            : [];
        DashboardFilterOptions filterOptions = await BuildFilterOptionsAsync(
            effectiveGuildId,
            effectiveUserId,
            channelDiscordId,
            cancellationToken);

        return new DashboardInsightsResponse(
            effectiveGuildId,
            effectiveUserId,
            channelDiscordId?.ToString(),
            safeDays,
            filters.Scope,
            normalizedSortDirection,
            safeMinActivity,
            activity,
            channels,
            users,
            heatmap,
            quotes,
            economy,
            stocks,
            buttonGame,
            operations,
            settings,
            filterOptions);
    }

    private static DashboardActivityInsights EmptyActivityInsights()
    {
        return new DashboardActivityInsights(0, 0, 0, 0, 0.0, 0.0, 0.0, 0, 0.0, []);
    }

    private static DashboardQuoteInsights EmptyQuoteInsights()
    {
        return new DashboardQuoteInsights(0, 0, 0, 0, 0, 0.0, [], [], [], []);
    }

    private static DashboardEconomyInsights EmptyEconomyInsights()
    {
        return new DashboardEconomyInsights(0m, 0m, 0m, 0, 0, 0m, 0m, [], [], [], []);
    }

    private static DashboardStockMarketInsights EmptyStockMarketInsights()
    {
        return new DashboardStockMarketInsights(0, 0m, 0.0, [], [], []);
    }

    private static DashboardButtonGameInsights EmptyButtonGameInsights()
    {
        return new DashboardButtonGameInsights(0, 0, 0.0, null, [], []);
    }

    private static DashboardOperationsInsights EmptyOperationsInsights()
    {
        return new DashboardOperationsInsights(
            new DashboardReminderStats(0, 0, 0, []),
            new DashboardModerationStats(0, 0, 0, []),
            [],
            [],
            []);
    }

    private async Task<IReadOnlyList<ScopedUserRow>> GetScopedUsersAsync(
        int? guildId,
        int? userId,
        CancellationToken cancellationToken)
    {
        IQueryable<User> query = dbContext.Users.AsNoTracking();

        if (userId.HasValue)
        {
            query = query.Where(user => user.Id == userId.Value);
        }
        else if (guildId.HasValue)
        {
            List<int> scopedUserIds = await dbContext.UserLevels
                .AsNoTracking()
                .Where(levels => levels.GuildId == guildId.Value)
                .Select(levels => levels.UserId)
                .Concat(dbContext.UserActivity
                    .AsNoTracking()
                    .Where(activity => activity.GuildId == guildId.Value)
                    .Select(activity => activity.UserId))
                .Distinct()
                .ToListAsync(cancellationToken);

            query = scopedUserIds.Count == 0
                ? query.Where(_ => false)
                : query.Where(user => scopedUserIds.Contains(user.Id));
        }

        return await query
            .OrderBy(user => user.Username)
            .Select(user => new ScopedUserRow(
                user.Id,
                user.DiscordId.ToString(),
                user.Username,
                user.Balance))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<UserActivity> BuildActivityQuery(
        DateTime? startDate,
        int? guildId,
        int? userId,
        ulong? channelDiscordId,
        DateTime? endExclusiveDate = null)
    {
        IQueryable<UserActivity> query = dbContext.UserActivity.AsNoTracking();

        if (startDate.HasValue)
            query = query.Where(activity => activity.InsertDate >= startDate.Value);

        if (endExclusiveDate.HasValue)
            query = query.Where(activity => activity.InsertDate < endExclusiveDate.Value);

        if (guildId.HasValue)
            query = query.Where(activity => activity.GuildId == guildId.Value);

        if (userId.HasValue)
            query = query.Where(activity => activity.UserId == userId.Value);

        if (channelDiscordId.HasValue)
            query = query.Where(activity => activity.DiscordChannelId == channelDiscordId.Value);

        return query;
    }

    private async Task<IReadOnlyList<DashboardGlobalServerActivity>> GetGlobalServerActivityAsync(
        DateTime? startDate,
        int limit,
        CancellationToken cancellationToken,
        DateTime? endExclusiveDate = null)
    {
        IQueryable<UserActivity> query = dbContext.UserActivity.AsNoTracking();
        if (startDate.HasValue)
            query = query.Where(activity => activity.InsertDate >= startDate.Value);
        if (endExclusiveDate.HasValue)
            query = query.Where(activity => activity.InsertDate < endExclusiveDate.Value);

        var rows = await query
            .GroupBy(activity => activity.GuildId)
            .Select(group => new
            {
                GuildId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                ActiveUsers = group.Select(activity => activity.UserId).Distinct().Count(),
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            })
            .OrderByDescending(row => row.Messages)
            .ThenByDescending(row => row.Xp)
            .Take(limit)
            .ToListAsync(cancellationToken);

        List<int> guildIds = [.. rows.Select(row => row.GuildId)];
        Dictionary<int, (string DiscordId, string Name)> guildLabels = guildIds.Count == 0
            ? []
            : await dbContext.Guilds
                .AsNoTracking()
                .Where(guild => guildIds.Contains(guild.Id))
                .Select(guild => new { guild.Id, guild.DiscordId, guild.Name })
                .ToDictionaryAsync(
                    guild => guild.Id,
                    guild => (guild.DiscordId.ToString(), guild.Name),
                    cancellationToken);

        return
        [
            .. rows.Select((row, index) =>
            {
                (string discordId, string name) = guildLabels.GetValueOrDefault(
                    row.GuildId,
                    (string.Empty, $"Server #{row.GuildId}"));

                return new DashboardGlobalServerActivity(
                    index + 1,
                    row.GuildId,
                    discordId,
                    name,
                    row.Messages,
                    row.Xp,
                    row.ActiveUsers,
                    row.LastActivityAtUtc);
            })
        ];
    }

    private async Task<IReadOnlyList<DashboardGlobalUserActivity>> GetGlobalUserActivityAsync(
        DateTime? startDate,
        string metric,
        int limit,
        CancellationToken cancellationToken,
        DateTime? endExclusiveDate = null)
    {
        IQueryable<UserActivity> query = dbContext.UserActivity.AsNoTracking();
        if (startDate.HasValue)
            query = query.Where(activity => activity.InsertDate >= startDate.Value);
        if (endExclusiveDate.HasValue)
            query = query.Where(activity => activity.InsertDate < endExclusiveDate.Value);

        var groupedUsers = query
            .GroupBy(activity => activity.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            });

        var rows = metric == "messages"
            ? await groupedUsers
                .OrderByDescending(row => row.Messages)
                .ThenByDescending(row => row.Xp)
                .Take(limit)
                .ToListAsync(cancellationToken)
            : await groupedUsers
                .OrderByDescending(row => row.Xp)
                .ThenByDescending(row => row.Messages)
                .Take(limit)
                .ToListAsync(cancellationToken);

        List<int> userIds = [.. rows.Select(row => row.UserId)];
        Dictionary<int, (string DiscordId, string Username)> labels = await GetUserLabelsAsync(userIds, cancellationToken);
        Dictionary<int, int> levels = await GetUserLevelsAsync(userIds, null, cancellationToken);

        return
        [
            .. rows.Select((row, index) =>
            {
                (string discordId, string username) = labels.GetValueOrDefault(row.UserId, (string.Empty, "Unknown"));
                return new DashboardGlobalUserActivity(
                    index + 1,
                    row.UserId,
                    discordId,
                    username,
                    row.Messages,
                    row.Xp,
                    levels.GetValueOrDefault(row.UserId),
                    row.LastActivityAtUtc);
            })
        ];
    }

    private async Task<IReadOnlyList<DashboardGlobalWealthUser>> GetGlobalWealthUsersAsync(
        bool orderByNetWorth,
        int limit,
        CancellationToken cancellationToken)
    {
        List<ScopedUserRow> users = await dbContext.Users
            .AsNoTracking()
            .Select(user => new ScopedUserRow(
                user.Id,
                user.DiscordId.ToString(),
                user.Username,
                user.Balance))
            .ToListAsync(cancellationToken);

        Dictionary<int, decimal> portfolios = await GetPortfolioValuesByUserAsync(
            users.Select(user => user.UserId),
            cancellationToken);

        var rankedUsers = users
            .Select(user =>
            {
                decimal portfolioValue = portfolios.GetValueOrDefault(user.UserId);
                return new
                {
                    User = user,
                    PortfolioValue = portfolioValue,
                    NetWorth = user.Balance + portfolioValue
                };
            });

        rankedUsers = orderByNetWorth
            ? rankedUsers.OrderByDescending(user => user.NetWorth).ThenByDescending(user => user.User.Balance)
            : rankedUsers.OrderByDescending(user => user.User.Balance).ThenByDescending(user => user.NetWorth);

        return
        [
            .. rankedUsers
                .Take(limit)
                .Select((user, index) => new DashboardGlobalWealthUser(
                    index + 1,
                    user.User.UserId,
                    user.User.DiscordId,
                    user.User.Username,
                    user.User.Balance,
                    user.PortfolioValue,
                    user.NetWorth))
        ];
    }

    private async Task<IReadOnlyList<DashboardPopularQuote>> GetPopularQuotesAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Quotes
            .AsNoTracking()
            .Where(quote => quote.Approved && !quote.Removed)
            .Select(quote => new
            {
                quote.Id,
                quote.GuildId,
                quote.UserId,
                Author = quote.User.Username,
                quote.Content,
                quote.InsertDate,
                Score = dbContext.QuoteScores
                    .Where(score => score.QuoteId == quote.Id)
                    .Sum(score => (int?)score.Score) ?? 0
            })
            .OrderByDescending(quote => quote.Score)
            .ThenByDescending(quote => quote.InsertDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select((quote, index) => new DashboardPopularQuote(
                index + 1,
                quote.Id,
                quote.GuildId,
                quote.UserId,
                quote.Author,
                quote.Content,
                quote.InsertDate,
                quote.Score))
        ];
    }

    private async Task<IReadOnlyList<DashboardGlobalChannelActivity>> GetGlobalChannelActivityAsync(
        DateTime? startDate,
        int limit,
        CancellationToken cancellationToken,
        DateTime? endExclusiveDate = null)
    {
        IQueryable<UserActivity> query = dbContext.UserActivity.AsNoTracking();
        if (startDate.HasValue)
            query = query.Where(activity => activity.InsertDate >= startDate.Value);
        if (endExclusiveDate.HasValue)
            query = query.Where(activity => activity.InsertDate < endExclusiveDate.Value);

        var rows = await query
            .GroupBy(activity => activity.DiscordChannelId)
            .Select(group => new
            {
                ChannelId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                ActiveUsers = group.Select(activity => activity.UserId).Distinct().Count(),
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            })
            .OrderByDescending(row => row.Messages)
            .ThenByDescending(row => row.Xp)
            .Take(limit)
            .ToListAsync(cancellationToken);

        Dictionary<ulong, string> channelLabels = await GetChannelLabelsAsync(
            rows.Select(row => row.ChannelId),
            cancellationToken);

        return
        [
            .. rows.Select((row, index) => new DashboardGlobalChannelActivity(
                index + 1,
                row.ChannelId.ToString(),
                channelLabels.GetValueOrDefault(row.ChannelId, $"channel-{ShortDiscordId(row.ChannelId)}"),
                row.Messages,
                row.Xp,
                row.ActiveUsers,
                row.LastActivityAtUtc))
        ];
    }

    private async Task<IReadOnlyList<DashboardRecentEntity>> GetRecentUsersAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await dbContext.Users
            .AsNoTracking()
            .OrderByDescending(user => user.InsertDate)
            .Take(limit)
            .Select(user => new DashboardRecentEntity(
                user.Id,
                user.DiscordId.ToString(),
                user.Username,
                user.InsertDate))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<DashboardRecentEntity>> GetRecentServersAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await dbContext.Guilds
            .AsNoTracking()
            .OrderByDescending(guild => guild.InsertDate)
            .Take(limit)
            .Select(guild => new DashboardRecentEntity(
                guild.Id,
                guild.DiscordId.ToString(),
                guild.Name,
                guild.InsertDate))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<DashboardRecentQuote>> GetRecentQuotesAsync(
        int limit,
        CancellationToken cancellationToken) =>
        await dbContext.Quotes
            .AsNoTracking()
            .OrderByDescending(quote => quote.InsertDate)
            .Take(limit)
            .Select(quote => new DashboardRecentQuote(
                quote.Id,
                quote.GuildId,
                quote.UserId,
                quote.User.Username,
                quote.Content,
                quote.Approved,
                quote.Removed,
                quote.InsertDate))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<DashboardRecentStock>> GetRecentStocksAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Stocks
            .AsNoTracking()
            .OrderByDescending(stock => stock.InsertDate)
            .Take(limit)
            .Select(stock => new
            {
                stock.Id,
                stock.EntityType,
                stock.EntityId,
                stock.Price,
                stock.DailyChangePercent,
                stock.InsertDate
            })
            .ToListAsync(cancellationToken);

        List<StockInsightRow> stockRows =
        [
            .. rows.Select(stock => new StockInsightRow(
                stock.Id,
                stock.EntityType,
                stock.EntityId,
                stock.Price,
                stock.DailyChangePercent))
        ];
        Dictionary<int, string> stockNames = await GetStockNamesAsync(stockRows, cancellationToken);

        return
        [
            .. rows.Select(stock => new DashboardRecentStock(
                stock.Id,
                EntityTypeLabel(stock.EntityType),
                stock.EntityId,
                stockNames.GetValueOrDefault(stock.Id, $"{EntityTypeLabel(stock.EntityType)} #{stock.EntityId}"),
                stock.Price,
                stock.DailyChangePercent,
                stock.InsertDate))
        ];
    }

    private async Task<IReadOnlyList<DashboardStackedServerActivityPoint>> BuildStackedServerActivityAsync(
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        int limit,
        CancellationToken cancellationToken)
    {
        var topServers = await dbContext.UserActivity
            .AsNoTracking()
            .Where(activity => activity.InsertDate >= startDate && activity.InsertDate < endExclusiveDate)
            .GroupBy(activity => activity.GuildId)
            .Select(group => new
            {
                GuildId = group.Key,
                Messages = group.Count()
            })
            .OrderByDescending(row => row.Messages)
            .Take(limit)
            .ToListAsync(cancellationToken);

        List<int> guildIds = [.. topServers.Select(server => server.GuildId)];
        if (guildIds.Count == 0)
            return [];

        Dictionary<int, string> guildNames = await dbContext.Guilds
            .AsNoTracking()
            .Where(guild => guildIds.Contains(guild.Id))
            .Select(guild => new { guild.Id, guild.Name })
            .ToDictionaryAsync(guild => guild.Id, guild => guild.Name, cancellationToken);

        var dailyRows = await dbContext.UserActivity
            .AsNoTracking()
            .Where(activity =>
                activity.InsertDate >= startDate &&
                activity.InsertDate < endExclusiveDate &&
                guildIds.Contains(activity.GuildId))
            .GroupBy(activity => new
            {
                Date = activity.InsertDate.Date,
                activity.GuildId
            })
            .Select(group => new
            {
                group.Key.Date,
                group.Key.GuildId,
                Messages = group.Count()
            })
            .ToListAsync(cancellationToken);

        Dictionary<(DateTime Date, int GuildId), int> messagesByDateAndGuild = dailyRows
            .ToDictionary(row => (row.Date.Date, row.GuildId), row => row.Messages);

        List<DashboardStackedServerActivityPoint> points = [];
        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            foreach (int guildId in guildIds)
            {
                points.Add(new DashboardStackedServerActivityPoint(
                    DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                    guildId,
                    guildNames.GetValueOrDefault(guildId, $"Server #{guildId}"),
                    messagesByDateAndGuild.GetValueOrDefault((date.Date, guildId))));
            }
        }

        return points;
    }

    private static IReadOnlyList<DashboardCalendarActivityCell> BuildCalendarActivity(
        IReadOnlyList<DashboardActivityDerivedPoint> activityPoints) =>
        [
            .. activityPoints.Select(point => new DashboardCalendarActivityCell(
                point.DateUtc,
                point.Messages,
                point.Xp,
                point.ActiveUsers))
        ];

    private async Task<IReadOnlyList<DashboardCategoryValue>> GetTransactionTypesAsync(
        DateTime startDate,
        DateTime endExclusiveDate,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.ProviderName == SqliteProviderName)
        {
            var sqliteRows = await dbContext.StockTransactions
                .AsNoTracking()
                .Where(transaction => transaction.InsertDate >= startDate && transaction.InsertDate < endExclusiveDate)
                .GroupBy(transaction => transaction.Type)
                .Select(group => new
                {
                    Type = group.Key,
                    Value = group.Sum(transaction => transaction.Amount < 0m
                        ? -(double)transaction.Amount
                        : (double)transaction.Amount)
                })
                .ToListAsync(cancellationToken);

            return
            [
                .. sqliteRows
                    .Select(group => new DashboardCategoryValue(
                        TransactionTypeLabel(group.Type),
                        (decimal)group.Value))
                    .OrderByDescending(item => item.Value)
            ];
        }

        var rows = await dbContext.StockTransactions
            .AsNoTracking()
            .Where(transaction => transaction.InsertDate >= startDate && transaction.InsertDate < endExclusiveDate)
            .GroupBy(transaction => transaction.Type)
            .Select(group => new
            {
                Type = group.Key,
                Value = group.Sum(transaction => transaction.Amount < 0m ? -transaction.Amount : transaction.Amount)
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows
                .Select(group => new DashboardCategoryValue(
                    TransactionTypeLabel(group.Type),
                    group.Value))
                .OrderByDescending(item => item.Value)
        ];
    }

    private async Task<IReadOnlyList<DashboardEconomyEventItem>> GetRecentEconomyEventsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.StockTransactions
            .AsNoTracking()
            .OrderByDescending(transaction => transaction.InsertDate)
            .Take(limit)
            .Select(transaction => new
            {
                transaction.Id,
                transaction.Type,
                transaction.Amount,
                transaction.Fee,
                transaction.UserId,
                User = transaction.User == null ? "Unknown" : transaction.User.Username,
                transaction.TargetUserId,
                TargetUser = transaction.TargetUser == null ? null : transaction.TargetUser.Username,
                transaction.StockId,
                transaction.InsertDate
            })
            .ToListAsync(cancellationToken);

        List<int> stockIds =
        [
            .. rows
                .Where(row => row.StockId.HasValue)
                .Select(row => row.StockId!.Value)
                .Distinct()
        ];
        List<StockInsightRow> stockRows = stockIds.Count == 0
            ? []
            : await dbContext.Stocks
                .AsNoTracking()
                .Where(stock => stockIds.Contains(stock.Id))
                .Select(stock => new StockInsightRow(
                    stock.Id,
                    stock.EntityType,
                    stock.EntityId,
                    stock.Price,
                    stock.DailyChangePercent))
                .ToListAsync(cancellationToken);
        Dictionary<int, string> stockNames = await GetStockNamesAsync(stockRows, cancellationToken);

        return
        [
            .. rows.Select(row => new DashboardEconomyEventItem(
                row.Id,
                TransactionTypeLabel(row.Type),
                row.Amount,
                row.Fee,
                row.UserId,
                row.User,
                row.TargetUserId,
                row.TargetUser,
                row.StockId,
                row.StockId.HasValue ? stockNames.GetValueOrDefault(row.StockId.Value) : null,
                row.InsertDate))
        ];
    }

    private async Task<IReadOnlyList<DashboardLogItem>> GetRecentBotHealthEventsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var logs = await dbContext.Logs
            .AsNoTracking()
            .Where(log => log.Severity <= 2)
            .OrderByDescending(log => log.InsertDate)
            .Take(limit)
            .Select(log => new
            {
                log.Id,
                log.Severity,
                log.Message,
                log.Version,
                log.InsertDate
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. logs.Select(log => new DashboardLogItem(
                log.Id,
                LogSeverityLabel(log.Severity),
                log.Message,
                log.Version,
                log.InsertDate))
        ];
    }

    private async Task<decimal> GetMoneySettingAmountAsync(
        string key,
        decimal fallback,
        CancellationToken cancellationToken)
    {
        List<string> values = await dbContext.BotSettings
            .AsNoTracking()
            .Where(setting => setting.Key == key)
            .OrderBy(setting => setting.Id)
            .Select(setting => setting.Value)
            .ToListAsync(cancellationToken);

        return values.Count == 0
            ? fallback
            : values.Sum(value => EconomyService.ParseMoneyFromStorage(value, fallback));
    }

    private async Task<DashboardFilterOptions> BuildFilterOptionsAsync(
        int? guildId,
        int? selectedUserId,
        ulong? selectedChannelDiscordId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DashboardUserOption> users = await BuildUserFilterOptionsAsync(
            guildId,
            selectedUserId,
            cancellationToken);
        IReadOnlyList<DashboardChannelOption> channels = await BuildChannelFilterOptionsAsync(
            guildId,
            selectedChannelDiscordId,
            cancellationToken);

        return new DashboardFilterOptions(users, channels);
    }

    private async Task<IReadOnlyList<DashboardUserOption>> BuildUserFilterOptionsAsync(
        int? guildId,
        int? selectedUserId,
        CancellationToken cancellationToken)
    {
        IQueryable<User> query = dbContext.Users.AsNoTracking();

        if (guildId.HasValue)
        {
            List<int> userIdRows = await dbContext.UserLevels
                .AsNoTracking()
                .Where(levels => levels.GuildId == guildId.Value)
                .Select(levels => levels.UserId)
                .Concat(dbContext.UserActivity
                    .AsNoTracking()
                    .Where(activity => activity.GuildId == guildId.Value)
                    .Select(activity => activity.UserId))
                .Distinct()
                .ToListAsync(cancellationToken);
            HashSet<int> userIds = [.. userIdRows];

            if (selectedUserId.HasValue)
                userIds.Add(selectedUserId.Value);

            query = userIds.Count == 0
                ? query.Where(_ => false)
                : query.Where(user => userIds.Contains(user.Id));
        }

        return await query
            .OrderBy(user => user.Username)
            .ThenBy(user => user.Id)
            .Select(user => new DashboardUserOption(
                user.Id,
                user.DiscordId.ToString(),
                user.Username))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<DashboardChannelOption>> BuildChannelFilterOptionsAsync(
        int? guildId,
        ulong? selectedChannelDiscordId,
        CancellationToken cancellationToken)
    {
        IQueryable<Channel> query = dbContext.Channels.AsNoTracking();

        if (guildId.HasValue)
        {
            List<ulong> channelDiscordIdRows = await dbContext.UserActivity
                .AsNoTracking()
                .Where(activity => activity.GuildId == guildId.Value)
                .Select(activity => activity.DiscordChannelId)
                .Distinct()
                .ToListAsync(cancellationToken);
            HashSet<ulong> channelDiscordIds = [.. channelDiscordIdRows];

            if (selectedChannelDiscordId.HasValue)
                channelDiscordIds.Add(selectedChannelDiscordId.Value);

            query = channelDiscordIds.Count == 0
                ? query.Where(_ => false)
                : query.Where(channel => channelDiscordIds.Contains(channel.DiscordId));
        }

        var channels = await query
            .OrderBy(channel => channel.Name)
            .Select(channel => new { channel.DiscordId, channel.Name })
            .ToListAsync(cancellationToken);

        return
        [
            .. channels
                .OrderBy(channel => channel.Name)
                .ThenBy(channel => channel.DiscordId)
                .Select(channel => new DashboardChannelOption(
                    channel.DiscordId.ToString(),
                    string.IsNullOrWhiteSpace(channel.Name)
                        ? $"channel-{ShortDiscordId(channel.DiscordId)}"
                        : channel.Name))
        ];
    }

    private async Task<DashboardActivityInsights> BuildActivityInsightsAsync(
        IQueryable<UserActivity> query,
        DateTime startDate,
        int days,
        CancellationToken cancellationToken)
    {
        List<DailyActivityAggregate> dailyRows = await query
            .GroupBy(activity => activity.InsertDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                ActiveUsers = group.Select(activity => activity.UserId).Distinct().Count()
            })
            .Select(row => new DailyActivityAggregate(row.Date, row.Messages, row.Xp, row.ActiveUsers))
            .ToListAsync(cancellationToken);

        Dictionary<DateTime, DailyActivityAggregate> byDate = dailyRows
            .ToDictionary(row => row.Date.Date);

        List<int> dailyMessages = [];
        List<DashboardActivityDerivedPoint> points = [];
        long cumulativeMessages = 0;
        long cumulativeXp = 0;

        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            int messages = 0;
            long xp = 0;
            int activeUsers = 0;
            if (byDate.TryGetValue(date, out DailyActivityAggregate? dayRow))
            {
                messages = dayRow.Messages;
                xp = dayRow.Xp;
                activeUsers = dayRow.ActiveUsers;
            }

            dailyMessages.Add(messages);
            int rollingStart = Math.Max(0, dailyMessages.Count - 7);
            double rollingMessages = dailyMessages
                .Skip(rollingStart)
                .Average();

            cumulativeMessages += messages;
            cumulativeXp += xp;

            points.Add(new DashboardActivityDerivedPoint(
                DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                messages,
                xp,
                activeUsers,
                Math.Round(rollingMessages, 1),
                cumulativeMessages,
                cumulativeXp));
        }

        long messagesTotal = await query.LongCountAsync(cancellationToken);
        long xpTotal = await query.SumAsync(activity => (long?)activity.XpGained, cancellationToken) ?? 0L;
        int activeUsersTotal = await query
            .Select(activity => activity.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
        int activeChannels = await query
            .Select(activity => activity.DiscordChannelId)
            .Distinct()
            .CountAsync(cancellationToken);
        double averageMessageLength = await query
            .AverageAsync(activity => (double?)activity.MessageLength, cancellationToken) ?? 0.0;
        double messagesPerActiveUser = activeUsersTotal == 0
            ? 0.0
            : (double)messagesTotal / activeUsersTotal;
        double xpPerMessage = messagesTotal == 0
            ? 0.0
            : (double)xpTotal / messagesTotal;
        var peakHour = messagesTotal == 0
            ? null
            : await query
                .GroupBy(activity => activity.InsertDate.Hour)
                .Select(group => new { Hour = group.Key, Messages = group.Count() })
                .OrderByDescending(group => group.Messages)
                .ThenBy(group => group.Hour)
                .FirstOrDefaultAsync(cancellationToken);
        int peakHourUtc = peakHour?.Hour ?? 0;

        double trendPercent = CalculateTrendPercent(dailyMessages);

        return new DashboardActivityInsights(
            messagesTotal,
            xpTotal,
            activeUsersTotal,
            activeChannels,
            Math.Round(averageMessageLength, 1),
            Math.Round(messagesPerActiveUser, 1),
            Math.Round(xpPerMessage, 1),
            peakHourUtc,
            trendPercent,
            points);
    }

    private async Task<IReadOnlyList<DashboardChannelActivity>> BuildChannelActivityAsync(
        IQueryable<UserActivity> query,
        string sortDirection,
        int minActivity,
        CancellationToken cancellationToken)
    {
        var channels = query
            .GroupBy(activity => activity.DiscordChannelId)
            .Select(group => new
            {
                ChannelId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                ActiveUsers = group.Select(activity => activity.UserId).Distinct().Count(),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0,
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            })
            .Where(channel => channel.Messages >= minActivity);

        var orderedChannels = sortDirection == "asc"
            ? channels.OrderBy(channel => channel.Messages)
            : channels.OrderByDescending(channel => channel.Messages);

        var channelRows = await orderedChannels
            .Take(12)
            .ToListAsync(cancellationToken);
        Dictionary<ulong, string> channelLabels = await GetChannelLabelsAsync(
            channelRows.Select(row => row.ChannelId),
            cancellationToken);
        var finalChannels = sortDirection == "asc"
            ? channelRows.OrderBy(channel => channel.Messages).ThenBy(channel => channel.ChannelId)
            : channelRows.OrderByDescending(channel => channel.Messages).ThenBy(channel => channel.ChannelId);

        return [.. finalChannels
            .Take(12)
            .Select((channel, index) => new DashboardChannelActivity(
                index + 1,
                channel.ChannelId.ToString(),
                channelLabels.GetValueOrDefault(channel.ChannelId, $"channel-{ShortDiscordId(channel.ChannelId)}"),
                channel.Messages,
                channel.Xp,
                channel.ActiveUsers,
                Math.Round(channel.AverageMessageLength, 1),
                channel.LastActivityAtUtc))];
    }

    private async Task<IReadOnlyList<DashboardUserActivitySummary>> BuildUserActivitySummariesAsync(
        IQueryable<UserActivity> query,
        int? guildId,
        DateTime startDate,
        DateTime endExclusiveDate,
        string sortDirection,
        int minActivity,
        CancellationToken cancellationToken)
    {
        var userGroups = query
            .GroupBy(activity => activity.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0,
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            })
            .Where(user => user.Messages >= minActivity);

        var orderedUsers = sortDirection == "asc"
            ? userGroups.OrderBy(user => user.Xp).ThenBy(user => user.UserId)
            : userGroups.OrderByDescending(user => user.Xp).ThenBy(user => user.UserId);

        var rankedUsers = await orderedUsers
            .Take(12)
            .ToListAsync(cancellationToken);
        List<int> userIds = [.. rankedUsers.Select(user => user.UserId)];

        Dictionary<int, (string DiscordId, string Username)> userLabels = await GetUserLabelsAsync(userIds, cancellationToken);
        Dictionary<int, int> levels = await GetUserLevelsAsync(userIds, guildId, cancellationToken);
        Dictionary<int, decimal> balances = await GetUserBalancesAsync(userIds, cancellationToken);
        Dictionary<int, decimal> portfolios = await GetPortfolioValuesByUserAsync(userIds, cancellationToken);
        Dictionary<int, int> quotes = await GetQuoteCountsByUserAsync(userIds, guildId, startDate, endExclusiveDate, cancellationToken);
        Dictionary<int, long> buttonScores = await GetButtonScoresByUserAsync(userIds, guildId, startDate, endExclusiveDate, cancellationToken);

        return [.. rankedUsers.Select((user, index) =>
        {
            (string discordId, string username) = userLabels.GetValueOrDefault(user.UserId, (string.Empty, "Unknown"));
            return new DashboardUserActivitySummary(
                index + 1,
                user.UserId,
                discordId,
                username,
                user.Messages,
                user.Xp,
                levels.GetValueOrDefault(user.UserId),
                quotes.GetValueOrDefault(user.UserId),
                balances.GetValueOrDefault(user.UserId),
                portfolios.GetValueOrDefault(user.UserId),
                buttonScores.GetValueOrDefault(user.UserId),
                Math.Round(user.AverageMessageLength, 1),
                user.LastActivityAtUtc);
        })];
    }

    private async Task<IReadOnlyList<DashboardHeatmapCell>> BuildHeatmapAsync(
        IQueryable<UserActivity> query,
        CancellationToken cancellationToken)
    {
        var hourlyRows = await query
            .GroupBy(activity => new
            {
                Date = activity.InsertDate.Date,
                Hour = activity.InsertDate.Hour
            })
            .Select(group => new
            {
                group.Key.Date,
                group.Key.Hour,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .ToListAsync(cancellationToken);

        var hourlyUsers = await query
            .GroupBy(activity => new
            {
                Date = activity.InsertDate.Date,
                Hour = activity.InsertDate.Hour,
                activity.UserId
            })
            .Select(group => new
            {
                group.Key.Date,
                group.Key.Hour,
                group.Key.UserId
            })
            .ToListAsync(cancellationToken);

        Dictionary<(int DayOfWeek, int Hour), int> activeUsers = hourlyUsers
            .GroupBy(row => ((int)row.Date.DayOfWeek, row.Hour))
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.UserId).Distinct().Count());

        Dictionary<(int DayOfWeek, int Hour), (int Messages, long Xp, int ActiveUsers)> groupedRows = hourlyRows
            .GroupBy(row => ((int)row.Date.DayOfWeek, row.Hour))
            .ToDictionary(
                group => group.Key,
                group => (
                    group.Sum(row => row.Messages),
                    group.Sum(row => row.Xp),
                    activeUsers.GetValueOrDefault(group.Key)));

        List<DashboardHeatmapCell> cells = [];
        for (int day = 0; day < 7; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                var cell = groupedRows.GetValueOrDefault((day, hour));
                cells.Add(new DashboardHeatmapCell(
                    day,
                    DayLabels[day],
                    hour,
                    cell.Messages,
                    cell.Xp,
                    cell.ActiveUsers));
            }
        }

        return cells;
    }

    private async Task<DashboardQuoteInsights> BuildQuoteInsightsAsync(
        int? guildId,
        int? userId,
        DateTime startDate,
        DateTime endExclusiveDate,
        CancellationToken cancellationToken)
    {
        IQueryable<Quote> query = dbContext.Quotes
            .AsNoTracking()
            .Where(quote => quote.InsertDate >= startDate && quote.InsertDate < endExclusiveDate);

        if (guildId.HasValue)
            query = query.Where(quote => quote.GuildId == guildId.Value);

        if (userId.HasValue)
            query = query.Where(quote => quote.UserId == userId.Value);

        List<QuoteInsightRow> quoteRows = await query
            .Select(quote => new QuoteInsightRow(
                quote.Id,
                quote.GuildId,
                quote.UserId,
                quote.User.DiscordId.ToString(),
                quote.User.Username,
                quote.Content,
                quote.InsertDate,
                quote.Approved,
                quote.Removed))
            .ToListAsync(cancellationToken);

        List<int> quoteIds = [.. quoteRows.Select(quote => quote.Id)];
        Dictionary<int, int> scoresByQuote = quoteIds.Count == 0
            ? []
            : await dbContext.QuoteScores
                .AsNoTracking()
                .Where(score => quoteIds.Contains(score.QuoteId))
                .GroupBy(score => score.QuoteId)
                .Select(group => new { QuoteId = group.Key, Score = group.Sum(score => score.Score) })
                .ToDictionaryAsync(row => row.QuoteId, row => row.Score, cancellationToken);

        List<QuoteApprovalMessage> approvalMessages = quoteIds.Count == 0
            ? []
            : await dbContext.QuoteApprovalMessages
                .AsNoTracking()
                .Where(approval => quoteIds.Contains(approval.QuoteId))
                .ToListAsync(cancellationToken);

        int approved = quoteRows.Count(quote => quote.Approved && !quote.Removed);
        int pending = quoteRows.Count(quote => !quote.Approved && !quote.Removed);
        int removed = quoteRows.Count(quote => quote.Removed);
        double averageScore = quoteRows.Count == 0
            ? 0.0
            : quoteRows.Average(quote => scoresByQuote.GetValueOrDefault(quote.Id));

        IReadOnlyList<DashboardQuoteStatusSlice> statuses =
        [
            new("Approved", approved),
            new("Pending", pending),
            new("Removed", removed)
        ];

        IReadOnlyList<DashboardQuoteAuthorSummary> authors =
        [
            .. quoteRows
                .Where(quote => quote.Approved && !quote.Removed)
                .GroupBy(quote => quote.UserId)
                .Select(group =>
                {
                    QuoteInsightRow first = group.First();
                    return new DashboardQuoteAuthorSummary(
                        first.UserId,
                        first.DiscordId,
                        first.Username,
                        group.Count(),
                        group.Sum(quote => scoresByQuote.GetValueOrDefault(quote.Id)));
                })
                .OrderByDescending(author => author.Quotes)
                .ThenByDescending(author => author.Score)
                .Take(8)
        ];

        IReadOnlyList<DashboardHistogramBucket> scoreHistogram = BuildQuoteScoreHistogram(
            quoteRows.Select(quote => scoresByQuote.GetValueOrDefault(quote.Id)));

        IReadOnlyList<DashboardQuoteItem> recentPending =
        [
            .. quoteRows
                .Where(quote => !quote.Approved && !quote.Removed)
                .OrderByDescending(quote => quote.InsertedAtUtc)
                .Take(6)
                .Select(quote => new DashboardQuoteItem(
                    quote.Id,
                    quote.GuildId,
                    quote.UserId,
                    quote.Username,
                    quote.Content,
                    quote.InsertedAtUtc,
                    quote.Approved,
                    quote.Removed,
                    scoresByQuote.GetValueOrDefault(quote.Id)))
        ];

        return new DashboardQuoteInsights(
            approved,
            pending,
            removed,
            approvalMessages.Count,
            approvalMessages.Count(approval => !approval.Approved),
            Math.Round(averageScore, 1),
            statuses,
            authors,
            scoreHistogram,
            recentPending);
    }

    private async Task<DashboardEconomyInsights> BuildEconomyInsightsAsync(
        IReadOnlyList<ScopedUserRow> scopedUsers,
        int? guildId,
        int? userId,
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        CancellationToken cancellationToken)
    {
        List<int> scopedUserIds = [.. scopedUsers.Select(user => user.UserId)];
        IQueryable<StockTransaction> transactionQuery = dbContext.StockTransactions
            .AsNoTracking()
            .Where(transaction => transaction.InsertDate >= startDate && transaction.InsertDate < endExclusiveDate);

        if (userId.HasValue)
        {
            transactionQuery = transactionQuery.Where(transaction =>
                transaction.UserId == userId.Value ||
                transaction.TargetUserId == userId.Value);
        }
        else if (guildId.HasValue)
        {
            transactionQuery = scopedUserIds.Count == 0
                ? transactionQuery.Where(_ => false)
                : transactionQuery.Where(transaction =>
                    scopedUserIds.Contains(transaction.UserId) ||
                    (transaction.TargetUserId.HasValue && scopedUserIds.Contains(transaction.TargetUserId.Value)));
        }

        List<TransactionInsightRow> transactions = await transactionQuery
            .Select(transaction => new TransactionInsightRow(
                transaction.UserId,
                transaction.TargetUserId,
                transaction.Type,
                transaction.Amount,
                transaction.Fee,
                transaction.InsertDate))
            .ToListAsync(cancellationToken);
        HashSet<int>? scopedUserIdSet = userId.HasValue || guildId.HasValue
            ? [.. scopedUserIds]
            : null;
        IReadOnlyList<ScopedTransactionInsightRow> scopedTransactions =
        [
            .. transactions.Select(transaction => new ScopedTransactionInsightRow(
                transaction,
                GetTransactionPerspective(transaction, scopedUserIdSet)))
        ];

        Dictionary<int, decimal> portfolios = await GetPortfolioValuesByUserAsync(scopedUserIds, cancellationToken);
        decimal cashBalance = scopedUsers.Sum(user => user.Balance);
        decimal portfolioValue = portfolios.Values.Sum();
        decimal transactionVolume = transactions.Sum(transaction => Math.Abs(transaction.Amount));
        decimal fees = scopedTransactions.Sum(transaction =>
            transaction.Perspective == TransactionPerspective.Target
                ? 0m
                : transaction.Transaction.Fee);

        IReadOnlyList<DashboardEconomyFlowPoint> dailyFlow = BuildEconomyDailyFlow(scopedTransactions, startDate, days);
        IReadOnlyList<DashboardCategoryValue> transactionTypes =
        [
            .. transactions
                .GroupBy(transaction => TransactionTypeLabel(transaction.Type))
                .Select(group => new DashboardCategoryValue(group.Key, group.Sum(transaction => Math.Abs(transaction.Amount))))
                .OrderByDescending(item => item.Value)
        ];

        IReadOnlyList<DashboardMoneyFlow> moneyFlows =
        [
            .. scopedTransactions
                .GroupBy(transaction => MoneyFlowFor(transaction.Transaction.Type, transaction.Perspective))
                .Select(group => new DashboardMoneyFlow(
                    group.Key.Source,
                    group.Key.Target,
                    group.Sum(transaction => Math.Abs(transaction.Transaction.Amount))))
                .Where(flow => flow.Value > 0m)
                .OrderByDescending(flow => flow.Value)
                .Take(8)
        ];

        IReadOnlyList<DashboardWealthUser> wealthLeaders =
        [
            .. scopedUsers
                .Select(user =>
                {
                    decimal userPortfolio = portfolios.GetValueOrDefault(user.UserId);
                    return new
                    {
                        User = user,
                        Portfolio = userPortfolio,
                        NetWorth = user.Balance + userPortfolio
                    };
                })
                .OrderByDescending(user => user.NetWorth)
                .Take(10)
                .Select((user, index) => new DashboardWealthUser(
                    index + 1,
                    user.User.UserId,
                    user.User.DiscordId,
                    user.User.Username,
                    user.User.Balance,
                    user.Portfolio,
                    user.NetWorth))
        ];

        return new DashboardEconomyInsights(
            cashBalance,
            portfolioValue,
            cashBalance + portfolioValue,
            scopedUsers.Count(user => user.Balance != 0m || portfolios.GetValueOrDefault(user.UserId) != 0m),
            CountActiveTransactionParticipants(transactions, scopedUserIdSet),
            transactionVolume,
            fees,
            dailyFlow,
            transactionTypes,
            moneyFlows,
            wealthLeaders);
    }

    private async Task<DashboardStockMarketInsights> BuildStockMarketInsightsAsync(
        int? guildId,
        int? userId,
        ulong? channelDiscordId,
        IReadOnlyList<int> scopedUserIds,
        IQueryable<UserActivity> activityQuery,
        CancellationToken cancellationToken)
    {
        List<StockInsightRow> stocks = await dbContext.Stocks
            .AsNoTracking()
            .Select(stock => new StockInsightRow(
                stock.Id,
                stock.EntityType,
                stock.EntityId,
                stock.Price,
                stock.DailyChangePercent))
            .ToListAsync(cancellationToken);

        HashSet<int> scopedUserSet = [.. scopedUserIds];
        HashSet<int> scopedChannelEntityIds = await GetScopedChannelEntityIdsAsync(
            guildId,
            channelDiscordId,
            activityQuery,
            cancellationToken);

        IEnumerable<StockInsightRow> scopedStocks = stocks;
        if (channelDiscordId.HasValue)
        {
            scopedStocks = scopedStocks.Where(stock =>
                stock.EntityType == StockEntityType.Channel &&
                scopedChannelEntityIds.Contains(stock.EntityId));
        }
        else if (userId.HasValue)
        {
            scopedStocks = scopedStocks.Where(stock =>
                stock.EntityType == StockEntityType.User &&
                stock.EntityId == userId.Value);
        }
        else if (guildId.HasValue)
        {
            scopedStocks = scopedStocks.Where(stock =>
                stock.EntityType == StockEntityType.Guild && stock.EntityId == guildId.Value ||
                stock.EntityType == StockEntityType.User && scopedUserSet.Contains(stock.EntityId) ||
                stock.EntityType == StockEntityType.Channel && scopedChannelEntityIds.Contains(stock.EntityId));
        }

        List<StockInsightRow> stockRows = [.. scopedStocks];
        List<int> stockIds = [.. stockRows.Select(stock => stock.StockId)];
        Dictionary<int, decimal> holdingValues = await GetHoldingValuesByStockAsync(stockIds, cancellationToken);
        Dictionary<int, string> stockNames = await GetStockNamesAsync(stockRows, cancellationToken);

        IReadOnlyList<DashboardStockMover> movers =
        [
            .. stockRows.Select(stock => new DashboardStockMover(
                stock.StockId,
                EntityTypeLabel(stock.EntityType),
                stockNames.GetValueOrDefault(stock.StockId, $"{EntityTypeLabel(stock.EntityType)} #{stock.EntityId}"),
                stock.Price,
                stock.DailyChangePercent,
                holdingValues.GetValueOrDefault(stock.StockId)))
        ];

        IReadOnlyList<DashboardCategoryValue> entityTypes =
        [
            .. stockRows
                .GroupBy(stock => EntityTypeLabel(stock.EntityType))
                .Select(group => new DashboardCategoryValue(group.Key, group.Count()))
                .OrderByDescending(item => item.Value)
        ];

        return new DashboardStockMarketInsights(
            stockRows.Count,
            holdingValues.Values.Sum(),
            stockRows.Count == 0 ? 0.0 : Math.Round((double)stockRows.Average(stock => stock.DailyChangePercent), 2),
            [.. movers.OrderByDescending(stock => stock.DailyChangePercent).Take(5)],
            [.. movers.OrderBy(stock => stock.DailyChangePercent).Take(5)],
            entityTypes);
    }

    private async Task<DashboardButtonGameInsights> BuildButtonGameInsightsAsync(
        int? guildId,
        int? userId,
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        CancellationToken cancellationToken)
    {
        IQueryable<ButtonGamePress> query = dbContext.ButtonGamePresses
            .AsNoTracking()
            .Where(press => press.InsertDate >= startDate && press.InsertDate < endExclusiveDate);

        if (guildId.HasValue)
            query = query.Where(press => press.GuildId == guildId.Value);

        if (userId.HasValue)
            query = query.Where(press => press.UserId == userId.Value);

        List<ButtonGameInsightRow> rows = await query
            .Select(press => new ButtonGameInsightRow(
                press.UserId,
                press.Score,
                press.InsertDate))
            .ToListAsync(cancellationToken);

        List<DashboardButtonGamePoint> daily = BuildButtonGameDaily(rows, startDate, days);
        List<int> userIds = [.. rows.Select(row => row.UserId).Distinct()];
        Dictionary<int, (string DiscordId, string Username)> userLabels = await GetUserLabelsAsync(userIds, cancellationToken);

        IReadOnlyList<DashboardButtonGameUser> leaders =
        [
            .. rows
                .GroupBy(row => row.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Presses = (long)group.Count(),
                    Score = group.Sum(row => row.Score),
                    LastPressAtUtc = group.Max(row => row.InsertDate)
                })
                .OrderByDescending(user => user.Score)
                .Take(8)
                .Select((user, index) =>
                {
                    (string discordId, string username) = userLabels.GetValueOrDefault(user.UserId, (string.Empty, "Unknown"));
                    return new DashboardButtonGameUser(
                        index + 1,
                        user.UserId,
                        discordId,
                        username,
                        user.Presses,
                        user.Score,
                        user.LastPressAtUtc);
                })
        ];

        long score = rows.Sum(row => row.Score);
        return new DashboardButtonGameInsights(
            rows.Count,
            score,
            rows.Count == 0 ? 0.0 : Math.Round((double)score / rows.Count, 1),
            rows.Count == 0 ? null : rows.Max(row => row.InsertDate),
            daily,
            leaders);
    }

    private async Task<DashboardOperationsInsights> BuildOperationsInsightsAsync(
        int? guildId,
        int? userId,
        ulong? channelDiscordId,
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        ulong? guildDiscordId = guildId.HasValue
            ? await dbContext.Guilds
                .AsNoTracking()
                .Where(guild => guild.Id == guildId.Value)
                .Select(guild => (ulong?)guild.DiscordId)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        ulong? userDiscordId = userId.HasValue
            ? await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == userId.Value)
                .Select(user => (ulong?)user.DiscordId)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        DashboardReminderStats reminders = await BuildReminderStatsAsync(
            guildId,
            userId,
            channelDiscordId,
            now,
            cancellationToken);
        DashboardModerationStats moderation = await BuildModerationStatsAsync(
            guildId,
            guildDiscordId,
            userId,
            userDiscordId,
            now,
            cancellationToken);

        List<LogInsightRow> logs = await dbContext.Logs
            .AsNoTracking()
            .Where(log => log.InsertDate >= startDate && log.InsertDate < endExclusiveDate)
            .Select(log => new LogInsightRow(
                log.Id,
                log.Severity,
                log.Message,
                log.Version,
                log.InsertDate))
            .ToListAsync(cancellationToken);

        IReadOnlyList<DashboardLogSeveritySlice> logSeverities =
        [
            .. logs
                .GroupBy(log => LogSeverityLabel(log.Severity))
                .Select(group => new DashboardLogSeveritySlice(group.Key, group.Count()))
                .OrderByDescending(slice => slice.Count)
        ];

        IReadOnlyList<DashboardLogPoint> logTimeline = BuildLogTimeline(logs, startDate, days);
        IReadOnlyList<DashboardLogItem> recentLogs =
        [
            .. logs
                .OrderByDescending(log => log.InsertedAtUtc)
                .Take(8)
                .Select(log => new DashboardLogItem(
                    log.Id,
                    LogSeverityLabel(log.Severity),
                    log.Message,
                    log.Version,
                    log.InsertedAtUtc))
        ];

        return new DashboardOperationsInsights(
            reminders,
            moderation,
            logSeverities,
            logTimeline,
            recentLogs);
    }

    private async Task<IReadOnlyList<DashboardGuildSettingsSummary>> BuildSettingsInsightsAsync(
        int? guildId,
        CancellationToken cancellationToken)
    {
        IQueryable<Guild> query = dbContext.Guilds.AsNoTracking();
        if (guildId.HasValue)
            query = query.Where(guild => guild.Id == guildId.Value);

        return await query
            .OrderBy(guild => guild.Name)
            .Select(guild => new DashboardGuildSettingsSummary(
                guild.Id,
                guild.Name,
                guild.Prefix,
                guild.LevelUpMessages,
                guild.LevelUpQuotes,
                guild.UseGlobalQuotes,
                guild.WelcomeMessages,
                guild.UseActivityRoles,
                guild.QuoteAddRequiredApprovals,
                guild.QuoteRemoveRequiredApprovals))
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<ulong, string>> GetChannelLabelsAsync(
        IEnumerable<ulong> channelIds,
        CancellationToken cancellationToken)
    {
        List<ulong> ids = [.. channelIds.Distinct()];
        if (ids.Count == 0)
            return [];

        var channels = await dbContext.Channels
            .AsNoTracking()
            .Where(channel => ids.Contains(channel.DiscordId))
            .Select(channel => new { channel.DiscordId, channel.Name })
            .ToListAsync(cancellationToken);

        return channels.ToDictionary(
            channel => channel.DiscordId,
            channel => string.IsNullOrWhiteSpace(channel.Name)
                ? $"channel-{ShortDiscordId(channel.DiscordId)}"
                : channel.Name);
    }

    private async Task<Dictionary<int, decimal>> GetUserBalancesAsync(
        IEnumerable<int> userIds,
        CancellationToken cancellationToken)
    {
        List<int> ids = [.. userIds.Distinct()];
        if (ids.Count == 0)
            return [];

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.Balance })
            .ToDictionaryAsync(user => user.Id, user => user.Balance, cancellationToken);
    }

    private async Task<Dictionary<int, decimal>> GetPortfolioValuesByUserAsync(
        IEnumerable<int> userIds,
        CancellationToken cancellationToken)
    {
        List<int> ids = [.. userIds.Distinct()];
        if (ids.Count == 0)
            return [];

        var holdingValues = await dbContext.StockHoldings
            .AsNoTracking()
            .Where(holding => ids.Contains(holding.UserId))
            .Join(
                dbContext.Stocks.AsNoTracking(),
                holding => holding.StockId,
                stock => stock.Id,
                (holding, stock) => new { holding.UserId, holding.Shares, stock.Price })
            .ToListAsync(cancellationToken);

        return holdingValues
            .GroupBy(holding => holding.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(holding => holding.Shares * holding.Price));
    }

    private async Task<Dictionary<int, int>> GetQuoteCountsByUserAsync(
        IEnumerable<int> userIds,
        int? guildId,
        DateTime startDate,
        DateTime endExclusiveDate,
        CancellationToken cancellationToken)
    {
        List<int> ids = [.. userIds.Distinct()];
        if (ids.Count == 0)
            return [];

        IQueryable<Quote> query = dbContext.Quotes
            .AsNoTracking()
            .Where(quote =>
                ids.Contains(quote.UserId) &&
                quote.InsertDate >= startDate &&
                quote.InsertDate < endExclusiveDate &&
                quote.Approved &&
                !quote.Removed);

        if (guildId.HasValue)
            query = query.Where(quote => quote.GuildId == guildId.Value);

        return await query
            .GroupBy(quote => quote.UserId)
            .Select(group => new { UserId = group.Key, Quotes = group.Count() })
            .ToDictionaryAsync(row => row.UserId, row => row.Quotes, cancellationToken);
    }

    private async Task<Dictionary<int, long>> GetButtonScoresByUserAsync(
        IEnumerable<int> userIds,
        int? guildId,
        DateTime startDate,
        DateTime endExclusiveDate,
        CancellationToken cancellationToken)
    {
        List<int> ids = [.. userIds.Distinct()];
        if (ids.Count == 0)
            return [];

        IQueryable<ButtonGamePress> query = dbContext.ButtonGamePresses
            .AsNoTracking()
            .Where(press =>
                ids.Contains(press.UserId) &&
                press.InsertDate >= startDate &&
                press.InsertDate < endExclusiveDate);

        if (guildId.HasValue)
            query = query.Where(press => press.GuildId == guildId.Value);

        return await query
            .GroupBy(press => press.UserId)
            .Select(group => new { UserId = group.Key, Score = group.Sum(press => press.Score) })
            .ToDictionaryAsync(row => row.UserId, row => row.Score, cancellationToken);
    }

    private static IReadOnlyList<DashboardHistogramBucket> BuildQuoteScoreHistogram(IEnumerable<int> scores)
    {
        int negative = 0;
        int neutral = 0;
        int low = 0;
        int medium = 0;
        int high = 0;

        foreach (int score in scores)
        {
            if (score < 0)
                negative++;
            else if (score == 0)
                neutral++;
            else if (score <= 5)
                low++;
            else if (score <= 15)
                medium++;
            else
                high++;
        }

        return
        [
            new DashboardHistogramBucket("< 0", negative),
            new DashboardHistogramBucket("0", neutral),
            new DashboardHistogramBucket("1-5", low),
            new DashboardHistogramBucket("6-15", medium),
            new DashboardHistogramBucket("16+", high)
        ];
    }

    private static IReadOnlyList<DashboardEconomyFlowPoint> BuildEconomyDailyFlow(
        IReadOnlyList<ScopedTransactionInsightRow> transactions,
        DateTime startDate,
        int days)
    {
        Dictionary<DateTime, List<ScopedTransactionInsightRow>> byDate = transactions
            .GroupBy(transaction => transaction.Transaction.InsertDate.Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        List<DashboardEconomyFlowPoint> points = [];
        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            List<ScopedTransactionInsightRow> rows = byDate.GetValueOrDefault(date, []);
            decimal inflow = rows
                .Where(transaction => IsTransactionInflow(transaction.Transaction.Type, transaction.Perspective))
                .Sum(transaction => Math.Abs(transaction.Transaction.Amount));
            decimal outflow = rows
                .Where(transaction => IsTransactionOutflow(transaction.Transaction.Type, transaction.Perspective))
                .Sum(transaction => Math.Abs(transaction.Transaction.Amount));

            points.Add(new DashboardEconomyFlowPoint(
                DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                inflow,
                outflow,
                inflow - outflow));
        }

        return points;
    }

    private async Task<HashSet<int>> GetScopedChannelEntityIdsAsync(
        int? guildId,
        ulong? channelDiscordId,
        IQueryable<UserActivity> activityQuery,
        CancellationToken cancellationToken)
    {
        List<ulong> channelDiscordIds = [];

        if (channelDiscordId.HasValue)
        {
            channelDiscordIds.Add(channelDiscordId.Value);
        }
        else if (guildId.HasValue)
        {
            channelDiscordIds.AddRange(await activityQuery
                .Select(activity => activity.DiscordChannelId)
                .Distinct()
                .ToListAsync(cancellationToken));
        }

        if (channelDiscordIds.Count == 0)
            return [];

        List<int> channelIds = await dbContext.Channels
            .AsNoTracking()
            .Where(channel => channelDiscordIds.Contains(channel.DiscordId))
            .Select(channel => channel.Id)
            .ToListAsync(cancellationToken);

        return [.. channelIds];
    }

    private async Task<Dictionary<int, decimal>> GetHoldingValuesByStockAsync(
        IEnumerable<int> stockIds,
        CancellationToken cancellationToken)
    {
        List<int> ids = [.. stockIds.Distinct()];
        if (ids.Count == 0)
            return [];

        var holdingValues = await dbContext.StockHoldings
            .AsNoTracking()
            .Where(holding => ids.Contains(holding.StockId))
            .Join(
                dbContext.Stocks.AsNoTracking(),
                holding => holding.StockId,
                stock => stock.Id,
                (holding, stock) => new { holding.StockId, holding.Shares, stock.Price })
            .ToListAsync(cancellationToken);

        return holdingValues
            .GroupBy(holding => holding.StockId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(holding => holding.Shares * holding.Price));
    }

    private async Task<Dictionary<int, string>> GetStockNamesAsync(
        IReadOnlyList<StockInsightRow> stocks,
        CancellationToken cancellationToken)
    {
        List<int> userIds = [.. stocks
            .Where(stock => stock.EntityType == StockEntityType.User)
            .Select(stock => stock.EntityId)
            .Distinct()];
        List<int> guildIds = [.. stocks
            .Where(stock => stock.EntityType == StockEntityType.Guild)
            .Select(stock => stock.EntityId)
            .Distinct()];
        List<int> channelIds = [.. stocks
            .Where(stock => stock.EntityType == StockEntityType.Channel)
            .Select(stock => stock.EntityId)
            .Distinct()];

        Dictionary<int, (string DiscordId, string Username)> userLabels = await GetUserLabelsAsync(userIds, cancellationToken);
        Dictionary<int, string> guildLabels = guildIds.Count == 0
            ? []
            : await dbContext.Guilds
                .AsNoTracking()
                .Where(guild => guildIds.Contains(guild.Id))
                .Select(guild => new { guild.Id, guild.Name })
                .ToDictionaryAsync(guild => guild.Id, guild => guild.Name, cancellationToken);
        Dictionary<int, string> channelLabels = channelIds.Count == 0
            ? []
            : await dbContext.Channels
                .AsNoTracking()
                .Where(channel => channelIds.Contains(channel.Id))
                .Select(channel => new { channel.Id, channel.Name, channel.DiscordId })
                .ToDictionaryAsync(
                    channel => channel.Id,
                    channel => string.IsNullOrWhiteSpace(channel.Name)
                        ? $"channel-{ShortDiscordId(channel.DiscordId)}"
                        : channel.Name,
                    cancellationToken);

        Dictionary<int, string> names = [];
        foreach (StockInsightRow stock in stocks)
        {
            string name = stock.EntityType switch
            {
                StockEntityType.User => userLabels.TryGetValue(stock.EntityId, out (string DiscordId, string Username) userLabel)
                    ? userLabel.Username
                    : $"User #{stock.EntityId}",
                StockEntityType.Guild => guildLabels.GetValueOrDefault(stock.EntityId, $"Guild #{stock.EntityId}"),
                StockEntityType.Channel => channelLabels.GetValueOrDefault(stock.EntityId, $"Channel #{stock.EntityId}"),
                _ => $"Stock #{stock.StockId}"
            };
            names[stock.StockId] = name;
        }

        return names;
    }

    private static List<DashboardButtonGamePoint> BuildButtonGameDaily(
        IReadOnlyList<ButtonGameInsightRow> rows,
        DateTime startDate,
        int days)
    {
        Dictionary<DateTime, List<ButtonGameInsightRow>> byDate = rows
            .GroupBy(row => row.InsertDate.Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        List<int> dailyPresses = [];
        List<DashboardButtonGamePoint> points = [];
        long cumulativeScore = 0;

        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            List<ButtonGameInsightRow> dayRows = byDate.GetValueOrDefault(date, []);
            int presses = dayRows.Count;
            long score = dayRows.Sum(row => row.Score);
            int activeUsers = dayRows.Select(row => row.UserId).Distinct().Count();
            dailyPresses.Add(presses);
            int rollingStart = Math.Max(0, dailyPresses.Count - 7);
            double rollingPresses = dailyPresses.Skip(rollingStart).Average();
            cumulativeScore += score;

            points.Add(new DashboardButtonGamePoint(
                DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                presses,
                score,
                activeUsers,
                Math.Round(rollingPresses, 1),
                cumulativeScore));
        }

        return points;
    }

    private async Task<DashboardReminderStats> BuildReminderStatsAsync(
        int? guildId,
        int? userId,
        ulong? channelDiscordId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        IQueryable<Reminder> query = dbContext.Reminders.AsNoTracking();

        if (guildId.HasValue)
            query = query.Where(reminder => reminder.GuildId == guildId.Value);

        if (userId.HasValue)
            query = query.Where(reminder => reminder.UserId == userId.Value);

        if (channelDiscordId.HasValue)
            query = query.Where(reminder => reminder.ChannelId == channelDiscordId.Value);

        var reminders = await query
            .Select(reminder => new
            {
                reminder.Id,
                reminder.ChannelId,
                reminder.Text,
                reminder.DueDate,
                Username = reminder.User == null ? "Unknown" : reminder.User.Username
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<DashboardReminderItem> upcoming =
        [
            .. reminders
                .Where(reminder => reminder.DueDate >= now)
                .OrderBy(reminder => reminder.DueDate)
                .Take(6)
                .Select(reminder => new DashboardReminderItem(
                    reminder.Id,
                    reminder.ChannelId.ToString(),
                    reminder.Text,
                    reminder.Username,
                    reminder.DueDate))
        ];

        return new DashboardReminderStats(
            reminders.Count,
            reminders.Count(reminder => reminder.DueDate < now),
            reminders.Count(reminder => reminder.DueDate >= now && reminder.DueDate <= now.AddDays(1)),
            upcoming);
    }

    private async Task<DashboardModerationStats> BuildModerationStatsAsync(
        int? guildId,
        ulong? guildDiscordId,
        int? userId,
        ulong? userDiscordId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        IQueryable<TemporaryBan> query = dbContext.TemporaryBans.AsNoTracking();

        if (guildId.HasValue)
        {
            query = guildDiscordId.HasValue
                ? query.Where(ban => ban.GuildId == guildDiscordId.Value)
                : query.Where(_ => false);
        }

        if (userId.HasValue)
        {
            query = userDiscordId.HasValue
                ? query.Where(ban => ban.UserId == userDiscordId.Value)
                : query.Where(_ => false);
        }

        List<TemporaryBan> pendingBans = await query
            .Where(ban => ban.UnbannedAt == null)
            .OrderBy(ban => ban.ExpiresAt)
            .ToListAsync(cancellationToken);

        int completedLast30Days = await query
            .CountAsync(ban => ban.UnbannedAt != null && ban.UnbannedAt >= now.AddDays(-30), cancellationToken);

        IReadOnlyList<DashboardTemporaryBanItem> pending =
        [
            .. pendingBans
                .Take(6)
                .Select(ban => new DashboardTemporaryBanItem(
                    ban.Id,
                    ban.GuildId.ToString(),
                    ban.UserId.ToString(),
                    ban.Reason,
                    ban.ExpiresAt))
        ];

        return new DashboardModerationStats(
            pendingBans.Count,
            pendingBans.Count(ban => ban.ExpiresAt < now),
            completedLast30Days,
            pending);
    }

    private static IReadOnlyList<DashboardLogPoint> BuildLogTimeline(
        IReadOnlyList<LogInsightRow> logs,
        DateTime startDate,
        int days)
    {
        Dictionary<DateTime, List<LogInsightRow>> byDate = logs
            .GroupBy(log => log.InsertedAtUtc.Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        List<DashboardLogPoint> points = [];
        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            List<LogInsightRow> rows = byDate.GetValueOrDefault(date, []);
            points.Add(new DashboardLogPoint(
                DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                rows.Count,
                rows.Count(log => log.Severity == 2),
                rows.Count(log => log.Severity <= 1)));
        }

        return points;
    }

    private static double CalculateTrendPercent(IReadOnlyList<int> dailyMessages)
    {
        if (dailyMessages.Count < 2)
            return 0.0;

        int split = Math.Max(1, dailyMessages.Count / 2);
        double previous = dailyMessages.Take(split).Average();
        double recent = dailyMessages.Skip(split).DefaultIfEmpty(0).Average();

        if (previous == 0.0)
            return recent == 0.0 ? 0.0 : 100.0;

        return Math.Round((recent - previous) / previous * 100.0, 1);
    }

    private static TransactionPerspective GetTransactionPerspective(
        TransactionInsightRow transaction,
        HashSet<int>? scopedUserIds)
    {
        if (scopedUserIds is null)
            return TransactionPerspective.Actor;

        bool actorInScope = scopedUserIds.Contains(transaction.UserId);
        bool targetInScope = transaction.TargetUserId.HasValue &&
            scopedUserIds.Contains(transaction.TargetUserId.Value);

        if (actorInScope && targetInScope && transaction.TargetUserId != transaction.UserId)
            return TransactionPerspective.Internal;

        return targetInScope && !actorInScope
            ? TransactionPerspective.Target
            : TransactionPerspective.Actor;
    }

    private static int CountActiveTransactionParticipants(
        IReadOnlyList<TransactionInsightRow> transactions,
        HashSet<int>? scopedUserIds)
    {
        HashSet<int> participants = [];
        foreach (TransactionInsightRow transaction in transactions)
        {
            AddParticipant(transaction.UserId);
            if (transaction.TargetUserId.HasValue)
                AddParticipant(transaction.TargetUserId.Value);
        }

        return participants.Count;

        void AddParticipant(int participantId)
        {
            if (scopedUserIds is null || scopedUserIds.Contains(participantId))
                participants.Add(participantId);
        }
    }

    private static bool IsTransactionInflow(TransactionType type, TransactionPerspective perspective) =>
        perspective switch
        {
            TransactionPerspective.Internal => type is TransactionType.Transfer
                or TransactionType.StockTransfer
                or TransactionType.RobberyWin
                or TransactionType.RobberyLoss,
            TransactionPerspective.Target => type is TransactionType.Transfer
                or TransactionType.StockTransfer
                or TransactionType.RobberyLoss,
            _ => type is TransactionType.StockSell
                or TransactionType.SlotsWin
                or TransactionType.RobberyWin
        };

    private static bool IsTransactionOutflow(TransactionType type, TransactionPerspective perspective) =>
        perspective switch
        {
            TransactionPerspective.Internal => type is TransactionType.Transfer
                or TransactionType.StockTransfer
                or TransactionType.RobberyWin
                or TransactionType.RobberyLoss,
            TransactionPerspective.Target => type is TransactionType.RobberyWin,
            _ => type is TransactionType.StockBuy
                or TransactionType.SlotsLoss
                or TransactionType.Donation
                or TransactionType.RobberyLoss
                or TransactionType.Transfer
                or TransactionType.StockTransfer
        };

    private static (string Source, string Target) MoneyFlowFor(TransactionType type, TransactionPerspective perspective)
    {
        if (perspective == TransactionPerspective.Internal)
        {
            return type switch
            {
                TransactionType.StockTransfer => ("Member portfolios", "Member portfolios"),
                TransactionType.RobberyWin or TransactionType.RobberyLoss => ("Member wallets", "Member wallets"),
                TransactionType.Transfer => ("Member wallets", "Member wallets"),
                _ => MoneyFlowFor(type, TransactionPerspective.Actor)
            };
        }

        if (perspective == TransactionPerspective.Target)
        {
            return type switch
            {
                TransactionType.Transfer => ("Member transfers", "Wallets"),
                TransactionType.StockTransfer => ("Member transfers", "Portfolios"),
                TransactionType.RobberyWin => ("Wallets", "Robbery"),
                TransactionType.RobberyLoss => ("Robbery", "Wallets"),
                _ => MoneyFlowFor(type, TransactionPerspective.Actor)
            };
        }

        return type switch
        {
            TransactionType.StockBuy => ("Wallets", "Stock market"),
            TransactionType.StockSell => ("Stock market", "Wallets"),
            TransactionType.Transfer => ("Wallets", "Member transfers"),
            TransactionType.SlotsWin => ("Slots", "Wallets"),
            TransactionType.SlotsLoss => ("Wallets", "Slots"),
            TransactionType.Donation => ("Wallets", "Community pool"),
            TransactionType.RobberyWin => ("Robbery", "Wallets"),
            TransactionType.RobberyLoss => ("Wallets", "Robbery"),
            TransactionType.StockTransfer => ("Portfolios", "Member transfers"),
            _ => ("Other", "Wallets")
        };
    }

    private static string TransactionTypeLabel(TransactionType type) =>
        type switch
        {
            TransactionType.StockBuy => "Stock buy",
            TransactionType.StockSell => "Stock sell",
            TransactionType.Transfer => "Transfer",
            TransactionType.SlotsWin => "Slots win",
            TransactionType.SlotsLoss => "Slots loss",
            TransactionType.Donation => "Donation",
            TransactionType.RobberyWin => "Robbery win",
            TransactionType.RobberyLoss => "Robbery loss",
            TransactionType.StockTransfer => "Stock transfer",
            _ => type.ToString()
        };

    private static string EntityTypeLabel(StockEntityType type) =>
        type switch
        {
            StockEntityType.User => "User",
            StockEntityType.Guild => "Server",
            StockEntityType.Channel => "Channel",
            _ => type.ToString()
        };

    private static string LogSeverityLabel(int severity) =>
        severity switch
        {
            0 => "Critical",
            1 => "Error",
            2 => "Warning",
            3 => "Info",
            4 => "Verbose",
            5 => "Debug",
            _ => $"Severity {severity}"
        };

    private static string NormalizeSortDirection(string? sortDirection) =>
        string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            ? "asc"
            : "desc";

    private static string NormalizeScope(string? scope, int? guildId, int? userId, ulong? channelId)
    {
        string? normalized = scope?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "global" => "global",
            "server" or "guild" => "server",
            "user" => "user",
            "channel" => "channel",
            _ => channelId.HasValue ? "channel" :
                userId.HasValue ? "user" :
                guildId.HasValue ? "server" :
                "global"
        };
    }

    private static DashboardScopeFilters NormalizeScopeFilters(
        string? scope,
        int? guildId,
        int? userId,
        string? channelId)
    {
        ulong? channelDiscordId = ParseDiscordId(channelId);
        string normalizedScope = NormalizeScope(scope, guildId, userId, channelDiscordId);

        return normalizedScope switch
        {
            "global" => new DashboardScopeFilters(null, null, null, "global"),
            "server" => new DashboardScopeFilters(guildId, null, null, "server"),
            "user" => new DashboardScopeFilters(guildId, userId, null, "user"),
            "channel" => new DashboardScopeFilters(guildId, null, channelDiscordId, "channel"),
            _ => new DashboardScopeFilters(null, null, null, "global")
        };
    }

    private static ulong? ParseDiscordId(string? value) =>
        ulong.TryParse(value, out ulong parsed) && parsed > 0UL ? parsed : null;

    private static string ShortDiscordId(ulong id)
    {
        string value = id.ToString();
        return value.Length <= 4 ? value : value[^4..];
    }

    private static readonly string[] DayLabels = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    private sealed record ScopedUserRow(
        int UserId,
        string DiscordId,
        string Username,
        decimal Balance);

    private sealed record DashboardScopeFilters(
        int? GuildId,
        int? UserId,
        ulong? ChannelDiscordId,
        string Scope);

    private sealed record DailyActivityAggregate(
        DateTime Date,
        int Messages,
        long Xp,
        int ActiveUsers);

    private sealed record QuoteInsightRow(
        int Id,
        int GuildId,
        int UserId,
        string DiscordId,
        string Username,
        string Content,
        DateTime InsertedAtUtc,
        bool Approved,
        bool Removed);

    private sealed record TransactionInsightRow(
        int UserId,
        int? TargetUserId,
        TransactionType Type,
        decimal Amount,
        decimal Fee,
        DateTime InsertDate);

    private sealed record ScopedTransactionInsightRow(
        TransactionInsightRow Transaction,
        TransactionPerspective Perspective);

    private enum TransactionPerspective
    {
        Actor,
        Target,
        Internal
    }

    private sealed record StockInsightRow(
        int StockId,
        StockEntityType EntityType,
        int EntityId,
        decimal Price,
        decimal DailyChangePercent);

    private sealed record ButtonGameInsightRow(
        int UserId,
        long Score,
        DateTime InsertDate);

    private sealed record LogInsightRow(
        long Id,
        int Severity,
        string Message,
        string Version,
        DateTime InsertedAtUtc);

    private sealed record DashboardDateRange(
        DateTime StartDateUtc,
        DateTime EndDateUtc,
        DateTime EndExclusiveUtc,
        int Days);

    private async Task<List<DashboardLeaderboardRow>> GetRecentLeaderboardRowsAsync(
        int? guildId,
        int? userId,
        ulong? channelDiscordId,
        string metric,
        int days,
        int limit,
        CancellationToken cancellationToken)
    {
        DateTime since = DateTime.UtcNow.AddDays(-days);
        return await GetActivityLeaderboardRowsAsync(
            guildId,
            userId,
            channelDiscordId,
            metric,
            since,
            limit,
            cancellationToken);
    }

    private async Task<List<DashboardLeaderboardRow>> GetActivityLeaderboardRowsAsync(
        int? guildId,
        int? userId,
        ulong? channelDiscordId,
        string metric,
        DateTime? startDate,
        int limit,
        CancellationToken cancellationToken,
        DateTime? endExclusiveDate = null)
    {
        IQueryable<UserActivity> query = BuildActivityQuery(startDate, guildId, userId, channelDiscordId, endExclusiveDate);

        if (metric == "messages")
        {
            var rows = await query
                .GroupBy(activity => activity.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Value = group.LongCount(),
                    LastActivityAtUtc = group.Max(activity => activity.InsertDate)
                })
                .OrderByDescending(row => row.Value)
                .ThenByDescending(row => row.LastActivityAtUtc)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return [.. rows.Select(row => new DashboardLeaderboardRow(row.UserId, row.Value, row.LastActivityAtUtc))];
        }

        var xpRows = await query
            .GroupBy(activity => activity.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Value = group.Sum(activity => (long)activity.XpGained),
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            })
            .OrderByDescending(row => row.Value)
            .ThenByDescending(row => row.LastActivityAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return [.. xpRows.Select(row => new DashboardLeaderboardRow(row.UserId, row.Value, row.LastActivityAtUtc))];
    }

    private async Task<List<DashboardLeaderboardRow>> GetAllTimeLeaderboardRowsAsync(
        int? guildId,
        int? userId,
        string metric,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<UserLevels> query = dbContext.UserLevels.AsNoTracking();

        if (guildId.HasValue)
            query = query.Where(levels => levels.GuildId == guildId.Value);

        if (userId.HasValue)
            query = query.Where(levels => levels.UserId == userId.Value);

        if (metric == "messages")
        {
            var rows = await query
                .GroupBy(levels => levels.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Value = group.Sum(levels => (long)levels.UserMessageCount)
                })
                .Where(row => row.Value > 0)
                .OrderByDescending(row => row.Value)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return [.. rows.Select(row => new DashboardLeaderboardRow(row.UserId, row.Value, null))];
        }

        var xpRows = await query
            .GroupBy(levels => levels.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Value = group.Sum(levels => (long)levels.TotalXp)
            })
            .Where(row => row.Value > 0)
            .OrderByDescending(row => row.Value)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return [.. xpRows.Select(row => new DashboardLeaderboardRow(row.UserId, row.Value, null))];
    }

    private async Task<Dictionary<int, (string DiscordId, string Username)>> GetUserLabelsAsync(
        IEnumerable<int> userIds,
        CancellationToken cancellationToken)
    {
        List<int> ids = [.. userIds.Distinct()];

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.DiscordId, user.Username })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            user => user.Id,
            user => (user.DiscordId.ToString(), user.Username));
    }

    private async Task<Dictionary<int, int>> GetUserLevelsAsync(
        IEnumerable<int> userIds,
        int? guildId,
        CancellationToken cancellationToken)
    {
        List<int> ids = [.. userIds.Distinct()];
        if (ids.Count == 0)
            return [];

        IQueryable<UserLevels> query = dbContext.UserLevels
            .AsNoTracking()
            .Where(levels => ids.Contains(levels.UserId));

        if (guildId.HasValue)
            query = query.Where(levels => levels.GuildId == guildId.Value);

        var levelRows = await query
            .GroupBy(levels => levels.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                TotalXp = group.Sum(levels => (long)levels.TotalXp)
            })
            .ToListAsync(cancellationToken);

        return levelRows.ToDictionary(
            row => row.UserId,
            row => ActivityLevelService.CalculateLevel(row.TotalXp));
    }

    private async Task<decimal> GetTotalBalanceAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.ProviderName == SqliteProviderName)
        {
            List<decimal> balances = await dbContext.Users
                .AsNoTracking()
                .Select(user => user.Balance)
                .ToListAsync(cancellationToken);

            return balances.Sum();
        }

        return await dbContext.Users
            .AsNoTracking()
            .SumAsync(user => (decimal?)user.Balance, cancellationToken) ?? 0m;
    }

    private async Task<decimal> GetStockPortfolioValueAsync(CancellationToken cancellationToken)
    {
        var holdingValues = dbContext.StockHoldings
            .AsNoTracking()
            .Join(
                dbContext.Stocks.AsNoTracking(),
                holding => holding.StockId,
                stock => stock.Id,
                (holding, stock) => new { holding.Shares, stock.Price });

        if (dbContext.Database.ProviderName == SqliteProviderName)
        {
            var sqliteValues = await holdingValues.ToListAsync(cancellationToken);
            return sqliteValues.Sum(holding => holding.Shares * holding.Price);
        }

        return await holdingValues
            .SumAsync(holding => (decimal?)(holding.Shares * holding.Price), cancellationToken) ?? 0m;
    }

    private DashboardDateRange ResolveDateRange(int days, DateTime? startDateUtc, DateTime? endDateUtc)
    {
        DateTime today = DateTime.UtcNow.Date;
        int safeDays = ClampDays(days);
        DateTime endDate = ToUtcDate(endDateUtc ?? today);
        DateTime startDate = startDateUtc.HasValue
            ? ToUtcDate(startDateUtc.Value)
            : endDate.AddDays(-(safeDays - 1));

        if (startDate > endDate)
            (startDate, endDate) = (endDate, startDate);

        if (endDate > today)
            endDate = today;

        if (startDate > endDate)
            startDate = endDate;

        int actualDays = (endDate - startDate).Days + 1;
        if (actualDays > safeDays)
        {
            startDate = endDate.AddDays(-(safeDays - 1));
            actualDays = safeDays;
        }

        return new DashboardDateRange(
            startDate,
            endDate,
            endDate.AddDays(1),
            actualDays);
    }

    private static DateTime ToUtcDate(DateTime date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

    private int ClampDays(int days)
    {
        int maxDays = Math.Max(1, options.MaxActivityDays);
        return Math.Clamp(days, 1, maxDays);
    }

    private static int ClampLimit(int limit)
    {
        if (limit <= 0)
            return DefaultLeaderboardLimit;

        return Math.Clamp(limit, 1, MaxLeaderboardLimit);
    }

    private static string NormalizeMetric(string metric) =>
        metric.Trim().ToLowerInvariant() switch
        {
            "message" or "messages" => "messages",
            _ => "xp"
        };
}
