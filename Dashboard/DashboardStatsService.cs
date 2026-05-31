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
        bool includeSummary = safeView == "summary";

        DashboardGlobalTotals totals = includeSummary || includeAll
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

        if (safeView == "servers" || includeSummary || includeAll)
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

        if (safeView == "users" || includeSummary || includeAll)
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

        if (safeView == "quotes" || includeSummary || includeAll)
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
                startDate,
                endExclusiveDate,
                safeDays,
                cancellationToken);
            biggestStockGainers = stockMarket.Winners;
            biggestStockLosers = stockMarket.Losers;
        }
        else if (includeSummary)
        {
            biggestStockGainers = await GetStockMoversAsync(winners: true, 5, cancellationToken);
            biggestStockLosers = await GetStockMoversAsync(winners: false, 5, cancellationToken);
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

        return view is "all" or "activity" or "servers" or "users" or "quotes" or "economy" or "stocks" or "operations"
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
        if (!TryParseDiscordId(channelId, out ulong? channelDiscordId))
        {
            return new DashboardActivitySeriesResponse(
                guildId,
                safeDays,
                BuildEmptyActivityPoints(startDate, safeDays));
        }

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
        DashboardDateRange? dateRange = days.HasValue || startDateUtc.HasValue || endDateUtc.HasValue
            ? ResolveDateRange(days ?? options.MaxActivityDays, startDateUtc, endDateUtc)
            : null;
        int? safeDays = dateRange?.Days;
        if (!TryParseDiscordId(channelId, out ulong? channelDiscordId))
        {
            return new DashboardLeaderboardResponse(guildId, normalizedMetric, safeDays, safeLimit, []);
        }

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
        bool includeServer = filters.Scope == "server" && filters.GuildId.HasValue && (includeAll || safeView == "summary");
        bool includeUserProfile = filters.Scope == "user" && filters.UserId.HasValue && (includeAll || safeView == "summary");
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

        IReadOnlyList<ScopedUserRow> scopedUsers = includeEconomy || includeStocks || includeServer
            ? await GetScopedUsersAsync(
                effectiveGuildId,
                effectiveUserId,
                channelDiscordId.HasValue ? activityQuery : null,
                cancellationToken)
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
        DashboardActivityAnalytics activityAnalytics = safeView == "activity"
            ? await BuildActivityAnalyticsAsync(
                effectiveGuildId,
                effectiveUserId,
                channelDiscordId,
                activityQuery,
                activity,
                startDate,
                endExclusiveDate,
                safeDays,
                safeMinActivity,
                cancellationToken)
            : EmptyActivityAnalytics();
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
                effectiveGuildId.HasValue || channelDiscordId.HasValue,
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
                startDate,
                endExclusiveDate,
                safeDays,
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
        DashboardServerInsights? server = includeServer
            ? await BuildServerInsightsAsync(
                effectiveGuildId!.Value,
                scopedUsers,
                activityQuery,
                startDate,
                endExclusiveDate,
                safeDays,
                safeMinActivity,
                cancellationToken)
            : null;
        DashboardUserProfileInsights? userProfile = includeUserProfile
            ? await BuildUserProfileInsightsAsync(
                effectiveUserId!.Value,
                effectiveGuildId,
                activityQuery,
                activity,
                startDate,
                endExclusiveDate,
                safeDays,
                cancellationToken)
            : null;
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
            activityAnalytics,
            channels,
            users,
            heatmap,
            quotes,
            economy,
            stocks,
            buttonGame,
            operations,
            settings,
            server,
            userProfile,
            filterOptions);
    }

    private static DashboardActivityInsights EmptyActivityInsights()
    {
        return new DashboardActivityInsights(0, 0, 0, 0, 0.0, 0.0, 0.0, 0, 0.0, []);
    }

    private static DashboardActivityAnalytics EmptyActivityAnalytics()
    {
        return new DashboardActivityAnalytics(
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            new DashboardUserActivityStreaks(0, null, null, 0, null, null, 0, 0),
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            []);
    }

    private static DashboardQuoteInsights EmptyQuoteInsights()
    {
        return new DashboardQuoteInsights(
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0.0,
            0.0,
            0.0,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            []);
    }

    private static DashboardEconomyInsights EmptyEconomyInsights()
    {
        return new DashboardEconomyInsights(
            TotalMoneySupply: 0m,
            CashBalance: 0m,
            PortfolioValue: 0m,
            NetWorth: 0m,
            AverageBalance: 0m,
            MedianBalance: 0m,
            ActiveWallets: 0,
            ActiveTraders: 0,
            TransactionVolume: 0m,
            TransactionCount: 0,
            Fees: 0m,
            TaxesCollected: 0m,
            UbiPoolSize: 0m,
            UbiDonations: 0m,
            WealthTaxImpact: 0m,
            TransfersVolume: 0m,
            UserToUserTransferVolume: 0m,
            Inflows: 0m,
            Outflows: 0m,
            RobberyWins: 0,
            RobberyLosses: 0,
            RobberySuccessRate: 0.0,
            SlotsWins: 0,
            SlotsLosses: 0,
            SlotsVaultSize: 0m,
            SlotsPayoutRatio: 0.0,
            MoneySupplyTrend: [],
            DailyFlow: [],
            UbiPoolTrend: [],
            SlotsVaultTrend: [],
            SlotsProfitLoss: [],
            TransactionVolumeTimeline: [],
            TransactionTypes: [],
            MoneyFlows: [],
            CashLeaders: [],
            WealthLeaders: [],
            BalanceDistribution: [],
            WealthInequality: [],
            TopDonors: [],
            BiggestRobberies: [],
            MostRobbedUsers: [],
            MostSuccessfulRobbers: [],
            RobberyOutcomes: [],
            BiggestSlotsWins: [],
            BiggestSlotsLosses: [],
            SlotsOutcomes: [],
            EconomyHeatmap: []);
    }

    private static DashboardStockMarketInsights EmptyStockMarketInsights()
    {
        return new DashboardStockMarketInsights(
            Stocks: 0,
            UserStocks: 0,
            ServerStocks: 0,
            ChannelStocks: 0,
            MarketValue: 0m,
            AveragePrice: 0m,
            AverageDailyChangePercent: 0.0,
            BuyVolume: 0m,
            SellVolume: 0m,
            StockTransferVolume: 0m,
            Winners: [],
            Losers: [],
            EntityTypes: [],
            MostValuableStocks: [],
            MostHeldStocks: [],
            MostTradedStocks: [],
            NewestStocks: [],
            DailyChangeHistogram: [],
            PriceMovement: [],
            HoldingsByUser: [],
            HoldingsTable: [],
            TradeVolumeTimeline: [],
            BuyVsSell: [],
            OwnershipConcentration: [],
            ActivityToPrice: []);
    }

    private static DashboardButtonGameInsights EmptyButtonGameInsights()
    {
        return new DashboardButtonGameInsights(
            Presses: 0,
            Score: 0,
            AverageScore: 0.0,
            MedianScore: 0.0,
            HighestScoreEver: 0,
            LastPressAtUtc: null,
            Daily: [],
            Leaders: [],
            TopGlobalScores: [],
            TopServerScores: [],
            TopIndividualScores: [],
            TopUsersByTotalScore: [],
            TopUsersByPressCount: [],
            ScoreDistribution: [],
            PressesByServer: [],
            PressesByHour: [],
            PressesByWeekday: [],
            HourByWeekdayHeatmap: [],
            CalendarHeatmap: [],
            LongestGaps: [],
            CompetitiveServers: []);
    }

    private static DashboardOperationsInsights EmptyOperationsInsights()
    {
        DashboardLogInsights logs = new(
            Total: 0,
            Warnings: 0,
            Errors: 0,
            Critical: 0,
            LatestAtUtc: null,
            SeverityCounts: [],
            Timeline: [],
            Recent: [],
            LogsByVersion: [],
            CommonMessages: [],
            RecentIncidents: [],
            HealthIndicators: []);

        return new DashboardOperationsInsights(
            new DashboardReminderStats(0, 0, 0, 0.0, [], [], [], [], [], [], []),
            new DashboardModerationStats(
                PendingTemporaryBans: 0,
                OverdueTemporaryBans: 0,
                CompletedLast30Days: 0,
                ReactionRoleMessages: 0,
                ReactionRoleItems: 0,
                Pending: [],
                TemporaryBanTimeline: [],
                BanStatus: [],
                BanReasons: [],
                ReactionRoleTypes: [],
                ReactionRoleUsage: [],
                ActivityRoleDistribution: [],
                ServerScorecards: [],
                IncompleteServerSetup: [],
                RiskyConfiguration: []),
            logs,
            [],
            [],
            []);
    }

    private async Task<IReadOnlyList<ScopedUserRow>> GetScopedUsersAsync(
        int? guildId,
        int? userId,
        IQueryable<UserActivity>? channelActivityQuery,
        CancellationToken cancellationToken)
    {
        IQueryable<User> query = dbContext.Users.AsNoTracking();

        if (userId.HasValue)
        {
            query = query.Where(user => user.Id == userId.Value);
        }
        else if (channelActivityQuery is not null)
        {
            List<int> scopedUserIds = await channelActivityQuery
                .Select(activity => activity.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            query = scopedUserIds.Count == 0
                ? query.Where(_ => false)
                : query.Where(user => scopedUserIds.Contains(user.Id));
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

    private async Task<IReadOnlyList<DashboardStockMover>> GetStockMoversAsync(
        bool winners,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<Stock> orderedStocks = winners
            ? dbContext.Stocks.AsNoTracking().OrderByDescending(stock => (double)stock.DailyChangePercent).ThenByDescending(stock => (double)stock.Price)
            : dbContext.Stocks.AsNoTracking().OrderBy(stock => (double)stock.DailyChangePercent).ThenByDescending(stock => (double)stock.Price);

        List<StockInsightRow> rows = await orderedStocks
            .Take(limit)
            .Select(stock => new StockInsightRow(
                stock.Id,
                stock.EntityType,
                stock.EntityId,
                stock.Price,
                stock.DailyChangePercent,
                stock.PreviousPrice,
                stock.InsertDate,
                stock.LastUpdatedDate))
            .ToListAsync(cancellationToken);

        Dictionary<int, string> stockNames = await GetStockNamesAsync(rows, cancellationToken);
        Dictionary<int, decimal> holdingValues = await GetHoldingValuesByStockAsync(
            rows.Select(stock => stock.StockId),
            cancellationToken);

        return
        [
            .. rows.Select(stock => new DashboardStockMover(
                stock.StockId,
                EntityTypeLabel(stock.EntityType),
                stockNames.GetValueOrDefault(stock.StockId, $"{EntityTypeLabel(stock.EntityType)} #{stock.EntityId}"),
                stock.Price,
                stock.DailyChangePercent,
                holdingValues.GetValueOrDefault(stock.StockId)))
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

    private async Task<DashboardActivityAnalytics> BuildActivityAnalyticsAsync(
        int? guildId,
        int? userId,
        ulong? channelDiscordId,
        IQueryable<UserActivity> query,
        DashboardActivityInsights activity,
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        int minActivity,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DailyActivityAggregate> dailyActivity = await BuildDailyActivityAggregatesAsync(
            query,
            startDate,
            days,
            cancellationToken);
        var userTrends = await BuildUserTrendInsightsAsync(
            query,
            startDate,
            endExclusiveDate,
            cancellationToken);

        IReadOnlyList<DashboardActivityDistributionPoint> xpByUser =
            await BuildUserActivityDistributionAsync(query, "xp", 12, cancellationToken);
        IReadOnlyList<DashboardActivityDistributionPoint> xpByChannel =
            await BuildChannelActivityDistributionAsync(query, "xp", 12, cancellationToken);
        IReadOnlyList<DashboardActivityDistributionPoint> xpByServer =
            await BuildServerActivityDistributionAsync(query, "xp", 12, cancellationToken);
        IReadOnlyList<DashboardActivityDistributionPoint> messageShareByUser =
            await BuildUserActivityDistributionAsync(query, "messages", 12, cancellationToken);
        IReadOnlyList<DashboardActivityDistributionPoint> messageShareByChannel =
            await BuildChannelActivityDistributionAsync(query, "messages", 12, cancellationToken);
        IReadOnlyList<DashboardActivityDistributionPoint> messageShareByServer =
            await BuildServerActivityDistributionAsync(query, "messages", 12, cancellationToken);

        IReadOnlyList<DashboardActivityComparisonSeries> comparisonSeries =
            await BuildActivityComparisonSeriesAsync(
                guildId,
                userId,
                channelDiscordId,
                query,
                activity,
                startDate,
                endExclusiveDate,
                days,
                cancellationToken);

        IReadOnlyList<DashboardActivityScatterPoint> userScatter =
            await BuildUserActivityScatterPointsAsync(query, 12, cancellationToken);
        IReadOnlyList<DashboardActivityScatterPoint> channelScatter =
            await BuildChannelActivityScatterPointsAsync(query, 12, cancellationToken);
        IReadOnlyList<DashboardActivityScatterPoint> serverScatter =
            await BuildServerActivityScatterPointsAsync(query, 12, cancellationToken);
        IReadOnlyList<DashboardActivityScatterPoint> scatterPoints =
        [
            .. userScatter,
            .. channelScatter,
            .. serverScatter
        ];

        IReadOnlyList<DashboardActivityLeaderboardSet> leaderboards =
            await BuildActivityLeaderboardSetsAsync(
                guildId,
                userId,
                query,
                userTrends.FastestRising,
                userTrends.Dropping,
                startDate,
                endExclusiveDate,
                days,
                minActivity,
                cancellationToken);

        return new DashboardActivityAnalytics(
            BuildDailyActiveUserBuckets(dailyActivity),
            await BuildPeriodActiveUserBucketsAsync(query, "week", cancellationToken),
            await BuildPeriodActiveUserBucketsAsync(query, "month", cancellationToken),
            BuildBestActivityDays(dailyActivity),
            BuildWorstActivityDays(dailyActivity),
            await BuildPeakHoursAsync(query, cancellationToken),
            await BuildPeakWeekdaysAsync(query, cancellationToken),
            BuildUserActivityStreaks(dailyActivity),
            comparisonSeries,
            xpByUser,
            xpByChannel,
            xpByServer,
            messageShareByUser,
            messageShareByChannel,
            messageShareByServer,
            await BuildMessageLengthHistogramAsync(query, cancellationToken),
            await BuildMessageLengthTrendAsync(query, startDate, days, cancellationToken),
            await BuildMessageLengthBoxPlotsAsync(query, cancellationToken),
            scatterPoints,
            scatterPoints,
            await BuildChannelHourHeatmapAsync(query, cancellationToken),
            await BuildServerDayHeatmapAsync(query, startDate, days, cancellationToken),
            await BuildChannelHeatmapAsync(query, startDate, days, cancellationToken),
            BuildParetoPoints(messageShareByUser),
            leaderboards,
            userTrends.RankMovement);
    }

    private static IReadOnlyList<DashboardTimeBucket> BuildDailyActiveUserBuckets(
        IReadOnlyList<DailyActivityAggregate> dailyActivity) =>
        [
            .. dailyActivity.Select(day => new DashboardTimeBucket(
                day.Date.ToString("yyyy-MM-dd"),
                (int)(day.Date - DateTime.UnixEpoch).TotalDays,
                day.Messages,
                day.Xp,
                day.ActiveUsers))
        ];

    private async Task<IReadOnlyList<DashboardTimeBucket>> BuildPeriodActiveUserBucketsAsync(
        IQueryable<UserActivity> query,
        string period,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .Select(activity => new
            {
                Date = activity.InsertDate.Date,
                activity.UserId,
                activity.XpGained
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return [];

        var groupedRows = rows
            .GroupBy(row => period == "month"
                ? new PeriodKey(row.Date.Year, row.Date.Month, 1, $"{row.Date:yyyy-MM}")
                : BuildWeekPeriodKey(row.Date))
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Period)
            .ThenBy(group => group.Key.SubPeriod)
            .Select(group => new DashboardTimeBucket(
                group.Key.Label,
                group.Key.Year * 10000 + group.Key.Period * 100 + group.Key.SubPeriod,
                group.Count(),
                group.Sum(row => (long)row.XpGained),
                group.Select(row => row.UserId).Distinct().Count()))
            .ToList();

        return groupedRows;
    }

    private async Task<IReadOnlyList<DashboardActivityComparisonSeries>> BuildActivityComparisonSeriesAsync(
        int? guildId,
        int? userId,
        ulong? channelDiscordId,
        IQueryable<UserActivity> query,
        DashboardActivityInsights selectedActivity,
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        CancellationToken cancellationToken)
    {
        List<DashboardActivityComparisonSeries> series =
        [
            new("selected-window", "Selected window", "time-range", selectedActivity.Points)
        ];

        DateTime previousStartDate = startDate.AddDays(-days);
        DashboardActivityInsights previousActivity = await BuildActivityInsightsAsync(
            BuildActivityQuery(previousStartDate, guildId, userId, channelDiscordId, startDate),
            previousStartDate,
            days,
            cancellationToken);
        series.Add(new DashboardActivityComparisonSeries(
            "previous-window",
            "Previous window",
            "time-range",
            previousActivity.Points));

        series.AddRange(await BuildTopUserComparisonSeriesAsync(query, startDate, days, cancellationToken));
        series.AddRange(await BuildTopChannelComparisonSeriesAsync(query, startDate, days, cancellationToken));
        series.AddRange(await BuildTopServerComparisonSeriesAsync(query, startDate, days, cancellationToken));

        return series;
    }

    private async Task<IReadOnlyList<DashboardActivityComparisonSeries>> BuildTopUserComparisonSeriesAsync(
        IQueryable<UserActivity> query,
        DateTime startDate,
        int days,
        CancellationToken cancellationToken)
    {
        List<int> topUserIds = await query
            .GroupBy(activity => activity.UserId)
            .Select(group => new { UserId = group.Key, Messages = group.Count() })
            .OrderByDescending(row => row.Messages)
            .ThenBy(row => row.UserId)
            .Take(4)
            .Select(row => row.UserId)
            .ToListAsync(cancellationToken);

        if (topUserIds.Count == 0)
            return [];

        var rows = await query
            .Where(activity => topUserIds.Contains(activity.UserId))
            .GroupBy(activity => new { Date = activity.InsertDate.Date, activity.UserId })
            .Select(group => new
            {
                group.Key.Date,
                group.Key.UserId,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .ToListAsync(cancellationToken);
        Dictionary<int, (string DiscordId, string Username)> labels =
            await GetUserLabelsAsync(topUserIds, cancellationToken);

        return
        [
            .. topUserIds.Select(userId =>
            {
                (string _, string username) = labels.GetValueOrDefault(userId, (string.Empty, $"User #{userId}"));
                IReadOnlyList<EntityDailyActivityRow> dailyRows =
                [
                    .. rows
                        .Where(row => row.UserId == userId)
                        .Select(row => new EntityDailyActivityRow(
                            userId.ToString(),
                            row.Date.Date,
                            row.Messages,
                            row.Xp,
                            row.Messages > 0 ? 1 : 0))
                ];

                return new DashboardActivityComparisonSeries(
                    $"user-{userId}",
                    username,
                    "user",
                    BuildDerivedActivityPoints(dailyRows, startDate, days));
            })
        ];
    }

    private async Task<IReadOnlyList<DashboardActivityComparisonSeries>> BuildTopChannelComparisonSeriesAsync(
        IQueryable<UserActivity> query,
        DateTime startDate,
        int days,
        CancellationToken cancellationToken)
    {
        List<ulong> topChannelIds = await query
            .GroupBy(activity => activity.DiscordChannelId)
            .Select(group => new { ChannelId = group.Key, Messages = group.Count() })
            .OrderByDescending(row => row.Messages)
            .Take(4)
            .Select(row => row.ChannelId)
            .ToListAsync(cancellationToken);

        if (topChannelIds.Count == 0)
            return [];

        var rows = await query
            .Where(activity => topChannelIds.Contains(activity.DiscordChannelId))
            .GroupBy(activity => new { Date = activity.InsertDate.Date, activity.DiscordChannelId })
            .Select(group => new
            {
                group.Key.Date,
                ChannelId = group.Key.DiscordChannelId,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                ActiveUsers = group.Select(activity => activity.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);
        Dictionary<ulong, string> labels = await GetChannelLabelsAsync(topChannelIds, cancellationToken);

        return
        [
            .. topChannelIds.Select(channelId =>
            {
                IReadOnlyList<EntityDailyActivityRow> dailyRows =
                [
                    .. rows
                        .Where(row => row.ChannelId == channelId)
                        .Select(row => new EntityDailyActivityRow(
                            channelId.ToString(),
                            row.Date.Date,
                            row.Messages,
                            row.Xp,
                            row.ActiveUsers))
                ];

                return new DashboardActivityComparisonSeries(
                    $"channel-{channelId}",
                    labels.GetValueOrDefault(channelId, $"channel-{ShortDiscordId(channelId)}"),
                    "channel",
                    BuildDerivedActivityPoints(dailyRows, startDate, days));
            })
        ];
    }

    private async Task<IReadOnlyList<DashboardActivityComparisonSeries>> BuildTopServerComparisonSeriesAsync(
        IQueryable<UserActivity> query,
        DateTime startDate,
        int days,
        CancellationToken cancellationToken)
    {
        List<int> topGuildIds = await query
            .GroupBy(activity => activity.GuildId)
            .Select(group => new { GuildId = group.Key, Messages = group.Count() })
            .OrderByDescending(row => row.Messages)
            .ThenBy(row => row.GuildId)
            .Take(4)
            .Select(row => row.GuildId)
            .ToListAsync(cancellationToken);

        if (topGuildIds.Count == 0)
            return [];

        var rows = await query
            .Where(activity => topGuildIds.Contains(activity.GuildId))
            .GroupBy(activity => new { Date = activity.InsertDate.Date, activity.GuildId })
            .Select(group => new
            {
                group.Key.Date,
                group.Key.GuildId,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                ActiveUsers = group.Select(activity => activity.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);
        Dictionary<int, string> labels = await GetGuildLabelsAsync(topGuildIds, cancellationToken);

        return
        [
            .. topGuildIds.Select(guildId =>
            {
                IReadOnlyList<EntityDailyActivityRow> dailyRows =
                [
                    .. rows
                        .Where(row => row.GuildId == guildId)
                        .Select(row => new EntityDailyActivityRow(
                            guildId.ToString(),
                            row.Date.Date,
                            row.Messages,
                            row.Xp,
                            row.ActiveUsers))
                ];

                return new DashboardActivityComparisonSeries(
                    $"server-{guildId}",
                    labels.GetValueOrDefault(guildId, $"Server #{guildId}"),
                    "server",
                    BuildDerivedActivityPoints(dailyRows, startDate, days));
            })
        ];
    }

    private static IReadOnlyList<DashboardActivityDerivedPoint> BuildDerivedActivityPoints(
        IReadOnlyList<EntityDailyActivityRow> rows,
        DateTime startDate,
        int days)
    {
        Dictionary<DateTime, EntityDailyActivityRow> byDate = rows.ToDictionary(row => row.Date.Date);
        List<int> dailyMessages = [];
        List<DashboardActivityDerivedPoint> points = [];
        long cumulativeMessages = 0L;
        long cumulativeXp = 0L;

        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset).Date;
            EntityDailyActivityRow? row = byDate.GetValueOrDefault(date);
            int messages = row?.Messages ?? 0;
            long xp = row?.Xp ?? 0L;
            int activeUsers = row?.ActiveUsers ?? 0;

            dailyMessages.Add(messages);
            int rollingStart = Math.Max(0, dailyMessages.Count - 7);
            double rollingMessages = dailyMessages.Skip(rollingStart).Average();
            cumulativeMessages += messages;
            cumulativeXp += xp;

            points.Add(new DashboardActivityDerivedPoint(
                DateTime.SpecifyKind(date, DateTimeKind.Utc),
                messages,
                xp,
                activeUsers,
                Math.Round(rollingMessages, 1),
                cumulativeMessages,
                cumulativeXp));
        }

        return points;
    }

    private async Task<IReadOnlyList<DashboardActivityDistributionPoint>> BuildUserActivityDistributionAsync(
        IQueryable<UserActivity> query,
        string metric,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .ToListAsync(cancellationToken);
        long total = metric == "messages"
            ? rows.Sum(row => row.Messages)
            : rows.Sum(row => row.Xp);
        Dictionary<int, (string DiscordId, string Username)> labels =
            await GetUserLabelsAsync(rows.Select(row => row.UserId), cancellationToken);

        return
        [
            .. rows
                .OrderByDescending(row => metric == "messages" ? row.Messages : row.Xp)
                .ThenBy(row => row.UserId)
                .Take(limit)
                .Select(row =>
                {
                    (string _, string username) = labels.GetValueOrDefault(row.UserId, (string.Empty, $"User #{row.UserId}"));
                    long value = metric == "messages" ? row.Messages : row.Xp;
                    return new DashboardActivityDistributionPoint(
                        row.UserId.ToString(),
                        username,
                        "user",
                        row.Messages,
                        row.Xp,
                        Percentage(value, total));
                })
        ];
    }

    private async Task<IReadOnlyList<DashboardActivityDistributionPoint>> BuildChannelActivityDistributionAsync(
        IQueryable<UserActivity> query,
        string metric,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.DiscordChannelId)
            .Select(group => new
            {
                ChannelId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .ToListAsync(cancellationToken);
        long total = metric == "messages"
            ? rows.Sum(row => row.Messages)
            : rows.Sum(row => row.Xp);
        Dictionary<ulong, string> labels = await GetChannelLabelsAsync(
            rows.Select(row => row.ChannelId),
            cancellationToken);

        return
        [
            .. rows
                .OrderByDescending(row => metric == "messages" ? row.Messages : row.Xp)
                .ThenBy(row => row.ChannelId)
                .Take(limit)
                .Select(row =>
                {
                    long value = metric == "messages" ? row.Messages : row.Xp;
                    return new DashboardActivityDistributionPoint(
                        row.ChannelId.ToString(),
                        labels.GetValueOrDefault(row.ChannelId, $"channel-{ShortDiscordId(row.ChannelId)}"),
                        "channel",
                        row.Messages,
                        row.Xp,
                        Percentage(value, total));
                })
        ];
    }

    private async Task<IReadOnlyList<DashboardActivityDistributionPoint>> BuildServerActivityDistributionAsync(
        IQueryable<UserActivity> query,
        string metric,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.GuildId)
            .Select(group => new
            {
                GuildId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .ToListAsync(cancellationToken);
        long total = metric == "messages"
            ? rows.Sum(row => row.Messages)
            : rows.Sum(row => row.Xp);
        Dictionary<int, string> labels = await GetGuildLabelsAsync(
            rows.Select(row => row.GuildId),
            cancellationToken);

        return
        [
            .. rows
                .OrderByDescending(row => metric == "messages" ? row.Messages : row.Xp)
                .ThenBy(row => row.GuildId)
                .Take(limit)
                .Select(row =>
                {
                    long value = metric == "messages" ? row.Messages : row.Xp;
                    return new DashboardActivityDistributionPoint(
                        row.GuildId.ToString(),
                        labels.GetValueOrDefault(row.GuildId, $"Server #{row.GuildId}"),
                        "server",
                        row.Messages,
                        row.Xp,
                        Percentage(value, total));
                })
        ];
    }

    private async Task<IReadOnlyList<DashboardActivityScatterPoint>> BuildUserActivityScatterPointsAsync(
        IQueryable<UserActivity> query,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0
            })
            .OrderByDescending(row => row.Xp)
            .ThenByDescending(row => row.Messages)
            .Take(limit)
            .ToListAsync(cancellationToken);
        Dictionary<int, (string DiscordId, string Username)> labels =
            await GetUserLabelsAsync(rows.Select(row => row.UserId), cancellationToken);

        return
        [
            .. rows.Select(row =>
            {
                (string _, string username) = labels.GetValueOrDefault(row.UserId, (string.Empty, $"User #{row.UserId}"));
                return new DashboardActivityScatterPoint(
                    row.UserId.ToString(),
                    username,
                    "user",
                    row.Messages,
                    row.Xp,
                    Math.Round(row.AverageMessageLength, 1),
                    row.Messages == 0L ? 0.0 : Math.Round((double)row.Xp / row.Messages, 2));
            })
        ];
    }

    private async Task<IReadOnlyList<DashboardActivityScatterPoint>> BuildChannelActivityScatterPointsAsync(
        IQueryable<UserActivity> query,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.DiscordChannelId)
            .Select(group => new
            {
                ChannelId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0
            })
            .OrderByDescending(row => row.Xp)
            .ThenByDescending(row => row.Messages)
            .Take(limit)
            .ToListAsync(cancellationToken);
        Dictionary<ulong, string> labels = await GetChannelLabelsAsync(
            rows.Select(row => row.ChannelId),
            cancellationToken);

        return
        [
            .. rows.Select(row => new DashboardActivityScatterPoint(
                row.ChannelId.ToString(),
                labels.GetValueOrDefault(row.ChannelId, $"channel-{ShortDiscordId(row.ChannelId)}"),
                "channel",
                row.Messages,
                row.Xp,
                Math.Round(row.AverageMessageLength, 1),
                row.Messages == 0L ? 0.0 : Math.Round((double)row.Xp / row.Messages, 2)))
        ];
    }

    private async Task<IReadOnlyList<DashboardActivityScatterPoint>> BuildServerActivityScatterPointsAsync(
        IQueryable<UserActivity> query,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.GuildId)
            .Select(group => new
            {
                GuildId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0
            })
            .OrderByDescending(row => row.Xp)
            .ThenByDescending(row => row.Messages)
            .Take(limit)
            .ToListAsync(cancellationToken);
        Dictionary<int, string> labels = await GetGuildLabelsAsync(
            rows.Select(row => row.GuildId),
            cancellationToken);

        return
        [
            .. rows.Select(row => new DashboardActivityScatterPoint(
                row.GuildId.ToString(),
                labels.GetValueOrDefault(row.GuildId, $"Server #{row.GuildId}"),
                "server",
                row.Messages,
                row.Xp,
                Math.Round(row.AverageMessageLength, 1),
                row.Messages == 0L ? 0.0 : Math.Round((double)row.Xp / row.Messages, 2)))
        ];
    }

    private async Task<IReadOnlyList<DashboardActivityBoxPlotPoint>> BuildMessageLengthBoxPlotsAsync(
        IQueryable<UserActivity> query,
        CancellationToken cancellationToken)
    {
        List<int> allLengths = await query
            .Select(activity => activity.MessageLength)
            .ToListAsync(cancellationToken);
        if (allLengths.Count == 0)
            return [];

        List<ulong> topChannelIds = await query
            .GroupBy(activity => activity.DiscordChannelId)
            .Select(group => new { ChannelId = group.Key, Messages = group.Count() })
            .OrderByDescending(row => row.Messages)
            .Take(5)
            .Select(row => row.ChannelId)
            .ToListAsync(cancellationToken);
        var channelRows = topChannelIds.Count == 0
            ? []
            : await query
                .Where(activity => topChannelIds.Contains(activity.DiscordChannelId))
                .Select(activity => new
                {
                    activity.DiscordChannelId,
                    activity.MessageLength
                })
                .ToListAsync(cancellationToken);
        Dictionary<ulong, string> labels = await GetChannelLabelsAsync(topChannelIds, cancellationToken);
        List<DashboardActivityBoxPlotPoint> plots =
        [
            CreateBoxPlot("All messages", "all", allLengths)
        ];

        plots.AddRange(topChannelIds
            .Select(channelId => CreateBoxPlot(
                labels.GetValueOrDefault(channelId, $"channel-{ShortDiscordId(channelId)}"),
                "channel",
                [.. channelRows
                    .Where(row => row.DiscordChannelId == channelId)
                    .Select(row => row.MessageLength)]))
            .Where(plot => plot.Count > 0));

        return plots;
    }

    private async Task<IReadOnlyList<DashboardChannelHourHeatmapCell>> BuildChannelHourHeatmapAsync(
        IQueryable<UserActivity> query,
        CancellationToken cancellationToken)
    {
        List<ulong> topChannelIds = await query
            .GroupBy(activity => activity.DiscordChannelId)
            .Select(group => new { ChannelId = group.Key, Messages = group.Count() })
            .OrderByDescending(row => row.Messages)
            .Take(8)
            .Select(row => row.ChannelId)
            .ToListAsync(cancellationToken);

        if (topChannelIds.Count == 0)
            return [];

        var rows = await query
            .Where(activity => topChannelIds.Contains(activity.DiscordChannelId))
            .GroupBy(activity => new
            {
                activity.DiscordChannelId,
                Hour = activity.InsertDate.Hour
            })
            .Select(group => new
            {
                ChannelId = group.Key.DiscordChannelId,
                group.Key.Hour,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                ActiveUsers = group.Select(activity => activity.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);
        Dictionary<ulong, string> labels = await GetChannelLabelsAsync(topChannelIds, cancellationToken);
        Dictionary<(ulong ChannelId, int Hour), (int Messages, long Xp, int ActiveUsers)> byChannelHour = rows.ToDictionary(
            row => (row.ChannelId, row.Hour),
            row => (row.Messages, row.Xp, row.ActiveUsers));

        List<DashboardChannelHourHeatmapCell> cells = [];
        foreach (ulong channelId in topChannelIds)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                var cell = byChannelHour.GetValueOrDefault((channelId, hour));
                cells.Add(new DashboardChannelHourHeatmapCell(
                    channelId.ToString(),
                    labels.GetValueOrDefault(channelId, $"channel-{ShortDiscordId(channelId)}"),
                    hour,
                    cell.Messages,
                    cell.Xp,
                    cell.ActiveUsers));
            }
        }

        return cells;
    }

    private async Task<IReadOnlyList<DashboardServerDayActivityCell>> BuildServerDayHeatmapAsync(
        IQueryable<UserActivity> query,
        DateTime startDate,
        int days,
        CancellationToken cancellationToken)
    {
        List<int> topGuildIds = await query
            .GroupBy(activity => activity.GuildId)
            .Select(group => new { GuildId = group.Key, Messages = group.Count() })
            .OrderByDescending(row => row.Messages)
            .Take(8)
            .Select(row => row.GuildId)
            .ToListAsync(cancellationToken);

        if (topGuildIds.Count == 0)
            return [];

        var rows = await query
            .Where(activity => topGuildIds.Contains(activity.GuildId))
            .GroupBy(activity => new
            {
                Date = activity.InsertDate.Date,
                activity.GuildId
            })
            .Select(group => new
            {
                group.Key.Date,
                group.Key.GuildId,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                ActiveUsers = group.Select(activity => activity.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);
        Dictionary<int, string> labels = await GetGuildLabelsAsync(topGuildIds, cancellationToken);
        Dictionary<(DateTime Date, int GuildId), (int Messages, long Xp, int ActiveUsers)> byDateAndGuild = rows.ToDictionary(
            row => (row.Date.Date, row.GuildId),
            row => (row.Messages, row.Xp, row.ActiveUsers));

        List<DashboardServerDayActivityCell> cells = [];
        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset).Date;
            foreach (int guildId in topGuildIds)
            {
                var cell = byDateAndGuild.GetValueOrDefault((date, guildId));
                cells.Add(new DashboardServerDayActivityCell(
                    DateTime.SpecifyKind(date, DateTimeKind.Utc),
                    guildId,
                    labels.GetValueOrDefault(guildId, $"Server #{guildId}"),
                    cell.Messages,
                    cell.Xp,
                    cell.ActiveUsers));
            }
        }

        return cells;
    }

    private static IReadOnlyList<DashboardActivityParetoPoint> BuildParetoPoints(
        IReadOnlyList<DashboardActivityDistributionPoint> rows)
    {
        decimal cumulative = 0m;
        List<DashboardActivityParetoPoint> points = [];

        foreach (DashboardActivityDistributionPoint row in rows.OrderByDescending(row => row.Messages))
        {
            decimal share = row.SharePercent;
            cumulative += share;
            points.Add(new DashboardActivityParetoPoint(
                row.Id,
                row.Label,
                row.Messages,
                share,
                Math.Min(100m, Math.Round(cumulative, 1))));
        }

        return points;
    }

    private async Task<IReadOnlyList<DashboardActivityLeaderboardSet>> BuildActivityLeaderboardSetsAsync(
        int? guildId,
        int? userId,
        IQueryable<UserActivity> query,
        IReadOnlyList<DashboardUserTrend> risingUsers,
        IReadOnlyList<DashboardUserTrend> fallingUsers,
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        int minActivity,
        CancellationToken cancellationToken)
    {
        List<DashboardActivityLeaderboardSet> sets =
        [
            await BuildAllTimeUserLeaderboardSetAsync(
                "global-xp",
                "Global XP",
                "xp",
                "XP",
                null,
                userId,
                minActivity,
                cancellationToken),
            await BuildServerScopedLeaderboardSetAsync(
                "server-xp",
                "Server XP",
                "xp",
                "XP",
                guildId,
                userId,
                query,
                minActivity,
                cancellationToken),
            await BuildRecentUserLeaderboardSetAsync(
                "recent-xp",
                $"XP in past {days} days",
                "xp",
                "XP",
                guildId,
                query,
                minActivity,
                cancellationToken),
            await BuildAllTimeUserLeaderboardSetAsync(
                "global-messages",
                "Global messages",
                "messages",
                "messages",
                null,
                userId,
                minActivity,
                cancellationToken),
            await BuildServerScopedLeaderboardSetAsync(
                "server-messages",
                "Server messages",
                "messages",
                "messages",
                guildId,
                userId,
                query,
                minActivity,
                cancellationToken),
            await BuildRecentUserLeaderboardSetAsync(
                "recent-messages",
                $"Messages in past {days} days",
                "messages",
                "messages",
                guildId,
                query,
                minActivity,
                cancellationToken),
            await BuildRecentUserLeaderboardSetAsync(
                "average-message-length",
                "Average message length",
                "average-message-length",
                "chars",
                guildId,
                query,
                minActivity,
                cancellationToken),
            await BuildAllTimeUserLeaderboardSetAsync(
                "weighted-average-message-length",
                "Weighted global average message length",
                "average-message-length",
                "chars",
                null,
                userId,
                minActivity,
                cancellationToken),
            await BuildFastestLevelGainersLeaderboardSetAsync(
                guildId,
                userId,
                query,
                cancellationToken),
            await BuildConsistentUsersLeaderboardSetAsync(
                guildId,
                query,
                startDate,
                endExclusiveDate,
                cancellationToken),
            await BuildActiveChannelsLeaderboardSetAsync(query, cancellationToken),
            await BuildActiveServersLeaderboardSetAsync(query, cancellationToken),
            BuildTrendLeaderboardSet(
                "rising-users",
                "Rising users",
                risingUsers,
                positive: true),
            BuildTrendLeaderboardSet(
                "falling-users",
                "Falling users",
                fallingUsers,
                positive: false)
        ];

        return sets;
    }

    private async Task<DashboardActivityLeaderboardSet> BuildAllTimeUserLeaderboardSetAsync(
        string key,
        string title,
        string metric,
        string unit,
        int? guildId,
        int? userId,
        int minActivity,
        CancellationToken cancellationToken)
    {
        IQueryable<UserLevels> query = dbContext.UserLevels.AsNoTracking();
        if (guildId.HasValue)
            query = query.Where(levels => levels.GuildId == guildId.Value);
        if (userId.HasValue)
            query = query.Where(levels => levels.UserId == userId.Value);

        var levelRows = await query
            .Select(levels => new
            {
                levels.UserId,
                Xp = (long)levels.TotalXp,
                Messages = (long)levels.UserMessageCount,
                levels.UserAverageMessageLength
            })
            .ToListAsync(cancellationToken);

        List<ActivityLeaderboardCandidate> candidates =
        [
            .. levelRows
                .GroupBy(row => row.UserId)
                .Select(group =>
                {
                    long messages = group.Sum(row => row.Messages);
                    long xp = group.Sum(row => row.Xp);
                    double averageLength = messages == 0L
                        ? 0.0
                        : group.Sum(row => row.UserAverageMessageLength * row.Messages) / messages;
                    decimal value = metric == "messages"
                        ? messages
                        : metric == "average-message-length"
                            ? (decimal)Math.Round(averageLength, 1)
                            : xp;

                    return new ActivityLeaderboardCandidate(
                        group.Key.ToString(),
                        group.Key,
                        null,
                        "user",
                        value,
                        messages,
                        xp,
                        Math.Round(averageLength, 1),
                        messages == 0L ? 0.0 : Math.Round((double)xp / messages, 2),
                        null,
                        null);
                })
                .Where(row => row.Messages >= minActivity || metric == "xp")
        ];

        return await MaterializeUserLeaderboardSetAsync(
            key,
            title,
            metric,
            unit,
            candidates,
            guildId,
            cancellationToken);
    }

    private async Task<DashboardActivityLeaderboardSet> BuildServerScopedLeaderboardSetAsync(
        string key,
        string title,
        string metric,
        string unit,
        int? guildId,
        int? userId,
        IQueryable<UserActivity> query,
        int minActivity,
        CancellationToken cancellationToken)
    {
        if (guildId.HasValue)
        {
            return await BuildAllTimeUserLeaderboardSetAsync(
                key,
                title,
                metric,
                unit,
                guildId,
                userId,
                minActivity,
                cancellationToken);
        }

        return metric == "messages"
            ? await BuildActiveServersLeaderboardSetAsync(query, key, title, "messages", "messages", cancellationToken)
            : await BuildActiveServersLeaderboardSetAsync(query, key, title, "xp", "XP", cancellationToken);
    }

    private async Task<DashboardActivityLeaderboardSet> BuildRecentUserLeaderboardSetAsync(
        string key,
        string title,
        string metric,
        string unit,
        int? guildId,
        IQueryable<UserActivity> query,
        int minActivity,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0,
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            })
            .ToListAsync(cancellationToken);

        List<ActivityLeaderboardCandidate> candidates =
        [
            .. rows
                .Where(row => row.Messages >= minActivity)
                .Select(row =>
                {
                    decimal value = metric == "messages"
                        ? row.Messages
                        : metric == "average-message-length"
                            ? (decimal)Math.Round(row.AverageMessageLength, 1)
                            : row.Xp;

                    return new ActivityLeaderboardCandidate(
                        row.UserId.ToString(),
                        row.UserId,
                        null,
                        "user",
                        value,
                        row.Messages,
                        row.Xp,
                        Math.Round(row.AverageMessageLength, 1),
                        row.Messages == 0L ? 0.0 : Math.Round((double)row.Xp / row.Messages, 2),
                        row.LastActivityAtUtc,
                        null);
                })
        ];

        return await MaterializeUserLeaderboardSetAsync(
            key,
            title,
            metric,
            unit,
            candidates,
            guildId,
            cancellationToken);
    }

    private async Task<DashboardActivityLeaderboardSet> BuildFastestLevelGainersLeaderboardSetAsync(
        int? guildId,
        int? userId,
        IQueryable<UserActivity> query,
        CancellationToken cancellationToken)
    {
        var recentRows = await query
            .GroupBy(activity => activity.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                RecentXp = group.Sum(activity => (long)activity.XpGained),
                Messages = group.LongCount(),
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            })
            .ToListAsync(cancellationToken);
        List<int> userIds = [.. recentRows.Select(row => row.UserId)];
        Dictionary<int, long> totalXp = await GetTotalXpByUserAsync(userIds, guildId, userId, cancellationToken);

        List<ActivityLeaderboardCandidate> candidates =
        [
            .. recentRows.Select(row =>
            {
                long currentXp = totalXp.GetValueOrDefault(row.UserId, row.RecentXp);
                long previousXp = Math.Max(0L, currentXp - row.RecentXp);
                int currentLevel = ActivityLevelService.CalculateLevel(currentXp);
                int previousLevel = ActivityLevelService.CalculateLevel(previousXp);
                int gained = Math.Max(0, currentLevel - previousLevel);

                return new ActivityLeaderboardCandidate(
                    row.UserId.ToString(),
                    row.UserId,
                    null,
                    "user",
                    gained,
                    row.Messages,
                    row.RecentXp,
                    0.0,
                    row.Messages == 0L ? 0.0 : Math.Round((double)row.RecentXp / row.Messages, 2),
                    row.LastActivityAtUtc,
                    null);
            })
        ];

        return await MaterializeUserLeaderboardSetAsync(
            "fastest-level-gainers",
            "Fastest level gainers",
            "levels",
            "levels",
            candidates,
            guildId,
            cancellationToken);
    }

    private async Task<DashboardActivityLeaderboardSet> BuildConsistentUsersLeaderboardSetAsync(
        int? guildId,
        IQueryable<UserActivity> query,
        DateTime startDate,
        DateTime endExclusiveDate,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .Select(activity => new
            {
                activity.UserId,
                Date = activity.InsertDate.Date,
                activity.XpGained
            })
            .ToListAsync(cancellationToken);
        int totalDays = Math.Max(1, (endExclusiveDate.Date - startDate.Date).Days);

        List<ActivityLeaderboardCandidate> candidates =
        [
            .. rows
                .GroupBy(row => row.UserId)
                .Select(group =>
                {
                    List<DateTime> dates = [.. group.Select(row => row.Date.Date).Distinct().OrderBy(date => date)];
                    int longestStreak = CalculateLongestStreak(dates);
                    long messages = group.LongCount();
                    long xp = group.Sum(row => (long)row.XpGained);
                    double consistency = Math.Round((double)dates.Count / totalDays * 100.0, 1);

                    return new ActivityLeaderboardCandidate(
                        group.Key.ToString(),
                        group.Key,
                        null,
                        "user",
                        (decimal)consistency,
                        messages,
                        xp,
                        longestStreak,
                        messages == 0L ? 0.0 : Math.Round((double)xp / messages, 2),
                        dates.Count == 0 ? null : dates[^1],
                        null);
                })
        ];

        return await MaterializeUserLeaderboardSetAsync(
            "most-consistent-users",
            "Most consistent users",
            "consistency",
            "%",
            candidates,
            guildId,
            cancellationToken);
    }

    private async Task<DashboardActivityLeaderboardSet> BuildActiveChannelsLeaderboardSetAsync(
        IQueryable<UserActivity> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.DiscordChannelId)
            .Select(group => new
            {
                ChannelId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0,
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            })
            .OrderByDescending(row => row.Messages)
            .Take(10)
            .ToListAsync(cancellationToken);
        Dictionary<ulong, string> labels = await GetChannelLabelsAsync(
            rows.Select(row => row.ChannelId),
            cancellationToken);
        List<DashboardActivityLeaderboardItem> items =
        [
            .. rows.Select((row, index) => new DashboardActivityLeaderboardItem(
                index + 1,
                row.ChannelId.ToString(),
                labels.GetValueOrDefault(row.ChannelId, $"channel-{ShortDiscordId(row.ChannelId)}"),
                "channel",
                row.Messages,
                "messages",
                row.Messages,
                row.Xp,
                null,
                Math.Round(row.AverageMessageLength, 1),
                row.Messages == 0L ? 0.0 : Math.Round((double)row.Xp / row.Messages, 2),
                row.LastActivityAtUtc,
                null))
        ];

        return new DashboardActivityLeaderboardSet(
            "most-active-channels",
            "Most active channels",
            "messages",
            "messages",
            items);
    }

    private async Task<DashboardActivityLeaderboardSet> BuildActiveServersLeaderboardSetAsync(
        IQueryable<UserActivity> query,
        CancellationToken cancellationToken) =>
        await BuildActiveServersLeaderboardSetAsync(
            query,
            "most-active-servers",
            "Most active servers",
            "messages",
            "messages",
            cancellationToken);

    private async Task<DashboardActivityLeaderboardSet> BuildActiveServersLeaderboardSetAsync(
        IQueryable<UserActivity> query,
        string key,
        string title,
        string metric,
        string unit,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.GuildId)
            .Select(group => new
            {
                GuildId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0,
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            })
            .ToListAsync(cancellationToken);
        Dictionary<int, string> labels = await GetGuildLabelsAsync(
            rows.Select(row => row.GuildId),
            cancellationToken);
        List<DashboardActivityLeaderboardItem> items =
        [
            .. rows
                .OrderByDescending(row => metric == "xp" ? row.Xp : row.Messages)
                .ThenBy(row => row.GuildId)
                .Take(10)
                .Select((row, index) => new DashboardActivityLeaderboardItem(
                    index + 1,
                    row.GuildId.ToString(),
                    labels.GetValueOrDefault(row.GuildId, $"Server #{row.GuildId}"),
                    "server",
                    metric == "xp" ? row.Xp : row.Messages,
                    unit,
                    row.Messages,
                    row.Xp,
                    null,
                    Math.Round(row.AverageMessageLength, 1),
                    row.Messages == 0L ? 0.0 : Math.Round((double)row.Xp / row.Messages, 2),
                    row.LastActivityAtUtc,
                    null))
        ];

        return new DashboardActivityLeaderboardSet(key, title, metric, unit, items);
    }

    private static DashboardActivityLeaderboardSet BuildTrendLeaderboardSet(
        string key,
        string title,
        IReadOnlyList<DashboardUserTrend> trends,
        bool positive)
    {
        IReadOnlyList<DashboardActivityLeaderboardItem> items =
        [
            .. trends.Select(trend => new DashboardActivityLeaderboardItem(
                trend.Rank,
                trend.UserId.ToString(),
                trend.Username,
                "user",
                trend.Delta,
                "messages",
                trend.RecentMessages,
                0,
                null,
                0.0,
                0.0,
                null,
                trend.DeltaPercent))
        ];

        return new DashboardActivityLeaderboardSet(
            key,
            title,
            positive ? "rising" : "falling",
            "messages",
            items);
    }

    private async Task<DashboardActivityLeaderboardSet> MaterializeUserLeaderboardSetAsync(
        string key,
        string title,
        string metric,
        string unit,
        IReadOnlyList<ActivityLeaderboardCandidate> candidates,
        int? guildId,
        CancellationToken cancellationToken)
    {
        List<ActivityLeaderboardCandidate> ordered =
        [
            .. candidates
                .OrderByDescending(row => row.Value)
                .ThenByDescending(row => row.Xp)
                .ThenByDescending(row => row.Messages)
                .Take(10)
        ];
        List<int> userIds = [.. ordered
            .Where(row => row.UserId.HasValue)
            .Select(row => row.UserId!.Value)];
        Dictionary<int, (string DiscordId, string Username)> labels =
            await GetUserLabelsAsync(userIds, cancellationToken);
        Dictionary<int, int> levels = await GetUserLevelsAsync(userIds, guildId, cancellationToken);

        IReadOnlyList<DashboardActivityLeaderboardItem> items =
        [
            .. ordered.Select((row, index) =>
            {
                string label = row.Label ?? row.EntityId;
                if (row.UserId.HasValue)
                {
                    (string _, string username) = labels.GetValueOrDefault(row.UserId.Value, (string.Empty, $"User #{row.UserId.Value}"));
                    label = username;
                }

                return new DashboardActivityLeaderboardItem(
                    index + 1,
                    row.EntityId,
                    label,
                    row.EntityType,
                    row.Value,
                    unit,
                    row.Messages,
                    row.Xp,
                    row.UserId.HasValue ? levels.GetValueOrDefault(row.UserId.Value) : null,
                    row.AverageMessageLength,
                    row.XpPerMessage,
                    row.LastActivityAtUtc,
                    row.DeltaPercent);
            })
        ];

        return new DashboardActivityLeaderboardSet(key, title, metric, unit, items);
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
        List<QuoteScoreInsightRow> scoreRows = quoteIds.Count == 0
            ? []
            : await dbContext.QuoteScores
                .AsNoTracking()
                .Where(score => quoteIds.Contains(score.QuoteId))
                .Select(score => new QuoteScoreInsightRow(
                    score.QuoteId,
                    score.UserId,
                    score.User.DiscordId.ToString(),
                    score.User.Username,
                    score.Score,
                    score.InsertDate,
                    score.UpdateDate))
                .ToListAsync(cancellationToken);
        Dictionary<int, QuoteScoreStats> scoreStatsByQuote = BuildQuoteScoreStats(scoreRows);

        IQueryable<QuoteApprovalMessage> approvalQuery = dbContext.QuoteApprovalMessages
            .AsNoTracking()
            .Where(approval => approval.InsertDate >= startDate && approval.InsertDate < endExclusiveDate);

        if (guildId.HasValue)
            approvalQuery = approvalQuery.Where(approval => approval.Quote!.GuildId == guildId.Value);

        if (userId.HasValue)
            approvalQuery = approvalQuery.Where(approval => approval.Quote!.UserId == userId.Value);

        var approvalRows = await approvalQuery
            .Select(approval => new
            {
                Approval = new QuoteApprovalInsightRow(
                    approval.Id,
                    approval.QuoteId,
                    approval.ApprovalMessageId,
                    approval.Score,
                    approval.InsertDate,
                    approval.Type,
                    approval.Approved),
                Quote = new QuoteInsightRow(
                    approval.Quote!.Id,
                    approval.Quote.GuildId,
                    approval.Quote.UserId,
                    approval.Quote.User.DiscordId.ToString(),
                    approval.Quote.User.Username,
                    approval.Quote.Content,
                    approval.Quote.InsertDate,
                    approval.Quote.Approved,
                    approval.Quote.Removed)
            })
            .ToListAsync(cancellationToken);

        List<QuoteApprovalInsightRow> approvalMessages = [.. approvalRows.Select(row => row.Approval)];
        List<QuoteInsightRow> approvalQuoteRows =
        [
            .. approvalRows
                .Select(row => row.Quote)
                .GroupBy(quote => quote.Id)
                .Select(group => group.First())
        ];
        List<int> approvalIds = [.. approvalMessages.Select(approval => approval.Id)];
        List<QuoteApprovalVoteInsightRow> approvalVotes = approvalIds.Count == 0
            ? []
            : [.. (await dbContext.QuoteApprovals
                .AsNoTracking()
                .Where(vote => approvalIds.Contains(vote.QuoteApprovalMessageId))
                .Select(vote => new { vote.QuoteApprovalMessageId, vote.UserId, vote.InsertDate })
                .ToListAsync(cancellationToken))
                .Select(vote => new QuoteApprovalVoteInsightRow(
                    vote.QuoteApprovalMessageId,
                    vote.UserId,
                    vote.InsertDate))];

        List<QuoteInsightRow> approvalContextQuoteRows =
        [
            .. quoteRows
                .Concat(approvalQuoteRows)
                .GroupBy(quote => quote.Id)
                .Select(group => group.First())
        ];
        List<int> quoteGuildIds = [.. approvalContextQuoteRows.Select(quote => quote.GuildId).Distinct()];
        List<GuildQuoteConfigRow> guildConfigs = await GetQuoteGuildConfigsAsync(
            guildId,
            userId.HasValue ? quoteGuildIds : null,
            cancellationToken);
        Dictionary<int, GuildQuoteConfigRow> guildConfigById = guildConfigs.ToDictionary(guild => guild.GuildId);
        DateTime now = DateTime.UtcNow;
        int approvalExpiryDays = Env.Get<int>("QUOTE_APPROVAL_EXPIRY_DAYS", 5);
        List<DashboardQuoteApprovalRequestItem> approvalRequestItems = BuildQuoteApprovalRequestItems(
            approvalMessages,
            approvalVotes,
            approvalContextQuoteRows,
            guildConfigById,
            approvalExpiryDays,
            now);

        int approved = quoteRows.Count(quote => quote.Approved && !quote.Removed);
        int pending = quoteRows.Count(quote => !quote.Approved && !quote.Removed);
        int removed = quoteRows.Count(quote => quote.Removed);
        int expiredApprovalRequests = approvalRequestItems.Count(request => request.Expired);
        int completedApprovalRequests = approvalMessages.Count(approval => approval.Approved);
        int pendingApprovalRequests = approvalRequestItems.Count(request => request.Status == "Pending");
        double approvalCompletionRate = approvalMessages.Count == 0
            ? 0.0
            : Math.Round((double)completedApprovalRequests / approvalMessages.Count * 100.0, 1);
        List<double> approvalDurations =
        [
            .. approvalRequestItems
                .Where(request => request.CompletedAtUtc.HasValue)
                .Select(request => Math.Max(0.0, (request.CompletedAtUtc!.Value - request.InsertedAtUtc).TotalHours))
        ];
        double averageApprovalTimeHours = approvalDurations.Count == 0
            ? 0.0
            : Math.Round(approvalDurations.Average(), 1);
        double averageScore = quoteRows.Count == 0
            ? 0.0
            : quoteRows.Average(quote => scoreStatsByQuote.GetValueOrDefault(quote.Id)?.Total ?? 0);

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
                        group.Sum(quote => scoreStatsByQuote.GetValueOrDefault(quote.Id)?.Total ?? 0));
                })
                .OrderByDescending(author => author.Quotes)
                .ThenByDescending(author => author.Score)
                .Take(8)
        ];

        IReadOnlyList<DashboardQuoteTimelinePoint> creationTimeline = BuildQuoteTimeline(
            quoteRows,
            scoreRows,
            approvalVotes,
            startDate,
            endExclusiveDate);
        IReadOnlyList<DashboardQuoteTimelinePoint> scoreTrend = BuildQuoteScoreTrend(
            scoreRows,
            startDate,
            endExclusiveDate);
        IReadOnlyList<DashboardCategoryValue> approvalFunnel =
        [
            new("Created", quoteRows.Count),
            new("Approved", approved),
            new("Pending", pending),
            new("Removed", removed),
            new("Approval requests", approvalMessages.Count),
            new("Completed", completedApprovalRequests),
            new("Expired", expiredApprovalRequests)
        ];
        IReadOnlyList<DashboardHistogramBucket> approvalTimeHistogram = BuildApprovalTimeHistogram(approvalDurations);
        IReadOnlyList<DashboardHistogramBucket> scoreHistogram = BuildQuoteScoreHistogram(
            quoteRows.Select(quote => scoreStatsByQuote.GetValueOrDefault(quote.Id)?.Total ?? 0));
        IReadOnlyList<DashboardCalendarActivityCell> approvalActivityCalendar = BuildQuoteApprovalCalendar(
            approvalVotes,
            startDate,
            endExclusiveDate);
        IReadOnlyList<DashboardQuoteServerSummary> serverSummaries = BuildQuoteServerSummaries(
            quoteRows,
            approvalRequestItems,
            scoreStatsByQuote,
            guildConfigById);
        IReadOnlyList<DashboardCategoryValue> globalVsServerUsage = BuildGlobalVsServerQuoteUsage(
            quoteRows,
            guildConfigs);
        IReadOnlyList<DashboardQuoteSetupSummary> setupSummaries =
        [
            .. guildConfigs
                .Select(config => BuildQuoteSetupSummary(config))
                .OrderBy(summary => summary.Health)
                .ThenBy(summary => summary.Name)
                .Take(12)
        ];
        IReadOnlyList<DashboardQuoteRankedItem> rankedQuotes = BuildRankedQuotes(
            quoteRows,
            scoreStatsByQuote,
            guildConfigById);
        IReadOnlyList<DashboardQuoteRankedItem> highestScoringQuotes =
        [
            .. rankedQuotes
                .Where(quote => quote.Approved && !quote.Removed)
                .OrderByDescending(quote => quote.Score)
                .ThenByDescending(quote => quote.TotalVotes)
                .Take(10)
                .Select((quote, index) => quote with { Rank = index + 1 })
        ];
        IReadOnlyList<DashboardQuoteRankedItem> lowestScoringQuotes =
        [
            .. rankedQuotes
                .Where(quote => quote.Approved && !quote.Removed)
                .OrderBy(quote => quote.Score)
                .ThenByDescending(quote => quote.TotalVotes)
                .Take(10)
                .Select((quote, index) => quote with { Rank = index + 1 })
        ];
        IReadOnlyList<DashboardQuoteRankedItem> mostControversialQuotes =
        [
            .. rankedQuotes
                .Where(quote => quote.TotalVotes > 0)
                .OrderByDescending(quote => quote.ControversyScore)
                .ThenByDescending(quote => quote.TotalVotes)
                .Take(10)
                .Select((quote, index) => quote with { Rank = index + 1 })
        ];
        IReadOnlyList<DashboardQuoteRankedItem> mostRemovedQuotes =
        [
            .. rankedQuotes
                .Where(quote => quote.Removed)
                .OrderByDescending(quote => quote.TotalVotes)
                .ThenBy(quote => quote.Score)
                .ThenByDescending(quote => quote.InsertedAtUtc)
                .Take(10)
                .Select((quote, index) => quote with { Rank = index + 1 })
        ];
        IReadOnlyList<DashboardQuoteCandidate> quoteOfTheDayCandidates = BuildQuoteCandidates(
            "Day",
            quoteRows,
            scoreStatsByQuote,
            guildConfigById,
            endExclusiveDate.AddDays(-1));
        IReadOnlyList<DashboardQuoteCandidate> quoteOfTheWeekCandidates = BuildQuoteCandidates(
            "Week",
            quoteRows,
            scoreStatsByQuote,
            guildConfigById,
            endExclusiveDate.AddDays(-7));
        IReadOnlyList<DashboardQuoteCandidate> quoteOfTheMonthCandidates = BuildQuoteCandidates(
            "Month",
            quoteRows,
            scoreStatsByQuote,
            guildConfigById,
            endExclusiveDate.AddDays(-30));
        IReadOnlyList<DashboardQuoteVoteItem> topVoters = BuildQuoteVoteItems(scoreRows);
        IReadOnlyList<DashboardQuoteVoteItem> approvalVoters = await BuildQuoteApprovalVoteItemsAsync(
            approvalVotes,
            cancellationToken);
        IReadOnlyList<DashboardQuoteManagementItem> quoteList = BuildQuoteManagementItems(
            quoteRows,
            scoreStatsByQuote,
            approvalRequestItems,
            guildConfigById,
            includeRemoved: true,
            limit: 50);
        IReadOnlyList<DashboardQuoteApprovalRequestItem> pendingApprovalQueue =
        [
            .. approvalRequestItems
                .Where(request => request.Status == "Pending")
                .OrderBy(request => request.ExpiresAtUtc)
                .ThenByDescending(request => request.InsertedAtUtc)
                .Take(20)
        ];
        IReadOnlyList<DashboardQuoteApprovalRequestItem> expiredApprovalQueue =
        [
            .. approvalRequestItems
                .Where(request => request.Expired)
                .OrderByDescending(request => request.InsertedAtUtc)
                .Take(20)
        ];
        IReadOnlyList<DashboardQuoteManagementItem> removedQuoteList = BuildQuoteManagementItems(
            quoteRows.Where(quote => quote.Removed),
            scoreStatsByQuote,
            approvalRequestItems,
            guildConfigById,
            includeRemoved: true,
            limit: 20);

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
                    scoreStatsByQuote.GetValueOrDefault(quote.Id)?.Total ?? 0))
        ];

        return new DashboardQuoteInsights(
            quoteRows.Count,
            approved,
            pending,
            removed,
            approvalMessages.Count,
            pendingApprovalRequests,
            expiredApprovalRequests,
            completedApprovalRequests,
            approvalCompletionRate,
            averageApprovalTimeHours,
            Math.Round(averageScore, 1),
            statuses,
            authors,
            creationTimeline,
            scoreTrend,
            approvalFunnel,
            approvalTimeHistogram,
            scoreHistogram,
            approvalActivityCalendar,
            serverSummaries,
            globalVsServerUsage,
            setupSummaries,
            highestScoringQuotes,
            lowestScoringQuotes,
            mostControversialQuotes,
            mostRemovedQuotes,
            quoteOfTheDayCandidates,
            quoteOfTheWeekCandidates,
            quoteOfTheMonthCandidates,
            topVoters,
            approvalVoters,
            quoteList,
            [.. approvalRequestItems.OrderByDescending(request => request.InsertedAtUtc).Take(50)],
            pendingApprovalQueue,
            expiredApprovalQueue,
            removedQuoteList,
            recentPending);
    }

    public async Task<DashboardQuoteDetailsResponse?> GetQuoteDetailsAsync(
        int quoteId,
        CancellationToken cancellationToken = default)
    {
        QuoteInsightRow? quote = await dbContext.Quotes
            .AsNoTracking()
            .Where(row => row.Id == quoteId)
            .Select(row => new QuoteInsightRow(
                row.Id,
                row.GuildId,
                row.UserId,
                row.User.DiscordId.ToString(),
                row.User.Username,
                row.Content,
                row.InsertDate,
                row.Approved,
                row.Removed))
            .FirstOrDefaultAsync(cancellationToken);

        if (quote == null)
            return null;

        List<QuoteScoreInsightRow> scoreRows = await dbContext.QuoteScores
            .AsNoTracking()
            .Where(score => score.QuoteId == quoteId)
            .Select(score => new QuoteScoreInsightRow(
                score.QuoteId,
                score.UserId,
                score.User.DiscordId.ToString(),
                score.User.Username,
                score.Score,
                score.InsertDate,
                score.UpdateDate))
            .ToListAsync(cancellationToken);
        Dictionary<int, QuoteScoreStats> scoreStatsByQuote = BuildQuoteScoreStats(scoreRows);
        List<QuoteApprovalInsightRow> approvalMessages = await dbContext.QuoteApprovalMessages
            .AsNoTracking()
            .Where(approval => approval.QuoteId == quoteId)
            .Select(approval => new QuoteApprovalInsightRow(
                approval.Id,
                approval.QuoteId,
                approval.ApprovalMessageId,
                approval.Score,
                approval.InsertDate,
                approval.Type,
                approval.Approved))
            .ToListAsync(cancellationToken);
        List<int> approvalIds = [.. approvalMessages.Select(approval => approval.Id)];
        List<QuoteApprovalVoteInsightRow> approvalVotes = approvalIds.Count == 0
            ? []
            : [.. (await dbContext.QuoteApprovals
                .AsNoTracking()
                .Where(vote => approvalIds.Contains(vote.QuoteApprovalMessageId))
                .Select(vote => new { vote.QuoteApprovalMessageId, vote.UserId, vote.InsertDate })
                .ToListAsync(cancellationToken))
                .Select(vote => new QuoteApprovalVoteInsightRow(
                    vote.QuoteApprovalMessageId,
                    vote.UserId,
                    vote.InsertDate))];
        Dictionary<int, GuildQuoteConfigRow> guildConfigById = (await GetQuoteGuildConfigsAsync(
                quote.GuildId,
                null,
                cancellationToken))
            .ToDictionary(guild => guild.GuildId);
        GuildQuoteConfigRow guildConfig = guildConfigById.GetValueOrDefault(quote.GuildId)
            ?? GuildQuoteConfigRow.Unknown(quote.GuildId);
        List<DashboardQuoteApprovalRequestItem> approvalRequests = BuildQuoteApprovalRequestItems(
            approvalMessages,
            approvalVotes,
            [quote],
            guildConfigById,
            Env.Get<int>("QUOTE_APPROVAL_EXPIRY_DAYS", 5),
            DateTime.UtcNow);

        return new DashboardQuoteDetailsResponse(
            quote.Id,
            quote.GuildId,
            quote.UserId,
            guildConfig.Name,
            quote.Content,
            quote.InsertedAtUtc,
            quote.Approved,
            quote.Removed,
            scoreStatsByQuote.GetValueOrDefault(quote.Id)?.Total ?? 0,
            quote.Username,
            BuildQuoteVoteItems(scoreRows),
            approvalRequests);
    }

    public async Task<DashboardQuoteApprovalRequestItem?> GetQuoteApprovalDetailsAsync(
        int approvalId,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.QuoteApprovalMessages
            .AsNoTracking()
            .Where(approval => approval.Id == approvalId)
            .Select(approval => new
            {
                Approval = new QuoteApprovalInsightRow(
                    approval.Id,
                    approval.QuoteId,
                    approval.ApprovalMessageId,
                    approval.Score,
                    approval.InsertDate,
                    approval.Type,
                    approval.Approved),
                Quote = new QuoteInsightRow(
                    approval.Quote!.Id,
                    approval.Quote.GuildId,
                    approval.Quote.UserId,
                    approval.Quote.User.DiscordId.ToString(),
                    approval.Quote.User.Username,
                    approval.Quote.Content,
                    approval.Quote.InsertDate,
                    approval.Quote.Approved,
                    approval.Quote.Removed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row == null)
            return null;

        List<QuoteApprovalVoteInsightRow> approvalVotes =
        [
            .. (await dbContext.QuoteApprovals
                .AsNoTracking()
                .Where(vote => vote.QuoteApprovalMessageId == approvalId)
                .Select(vote => new { vote.QuoteApprovalMessageId, vote.UserId, vote.InsertDate })
                .ToListAsync(cancellationToken))
                .Select(vote => new QuoteApprovalVoteInsightRow(
                    vote.QuoteApprovalMessageId,
                    vote.UserId,
                    vote.InsertDate))
        ];

        Dictionary<int, GuildQuoteConfigRow> guildConfigById = (await GetQuoteGuildConfigsAsync(
                row.Quote.GuildId,
                null,
                cancellationToken))
            .ToDictionary(guild => guild.GuildId);

        return BuildQuoteApprovalRequestItems(
                [row.Approval],
                approvalVotes,
                [row.Quote],
                guildConfigById,
                Env.Get<int>("QUOTE_APPROVAL_EXPIRY_DAYS", 5),
                DateTime.UtcNow)
            .SingleOrDefault();
    }

    private async Task<List<GuildQuoteConfigRow>> GetQuoteGuildConfigsAsync(
        int? guildId,
        IReadOnlyCollection<int>? guildIds,
        CancellationToken cancellationToken)
    {
        IQueryable<Guild> query = dbContext.Guilds.AsNoTracking();

        if (guildId.HasValue)
            query = query.Where(guild => guild.Id == guildId.Value);
        else if (guildIds is { Count: > 0 })
            query = query.Where(guild => guildIds.Contains(guild.Id));
        else if (guildIds is { Count: 0 })
            query = query.Where(_ => false);

        return await query
            .Select(guild => new GuildQuoteConfigRow(
                guild.Id,
                guild.DiscordId.ToString(),
                guild.Name,
                guild.UseGlobalQuotes,
                guild.QuotesApprovalChannelId != 0,
                guild.QuoteAddRequiredApprovals,
                guild.QuoteRemoveRequiredApprovals))
            .ToListAsync(cancellationToken);
    }

    private static Dictionary<int, QuoteScoreStats> BuildQuoteScoreStats(IEnumerable<QuoteScoreInsightRow> scoreRows) =>
        scoreRows
            .GroupBy(score => score.QuoteId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    List<QuoteScoreInsightRow> rows = [.. group];
                    return new QuoteScoreStats(
                        rows.Sum(score => score.Score),
                        rows.Count(score => score.Score > 0),
                        rows.Count(score => score.Score < 0),
                        rows.Count,
                        rows.Count == 0
                            ? null
                            : rows.Max(score => score.UpdatedAtUtc ?? score.InsertedAtUtc));
                });

    private static List<DashboardQuoteApprovalRequestItem> BuildQuoteApprovalRequestItems(
        IReadOnlyList<QuoteApprovalInsightRow> approvalMessages,
        IReadOnlyList<QuoteApprovalVoteInsightRow> approvalVotes,
        IReadOnlyList<QuoteInsightRow> quoteRows,
        IReadOnlyDictionary<int, GuildQuoteConfigRow> guildConfigById,
        int approvalExpiryDays,
        DateTime now)
    {
        Dictionary<int, QuoteInsightRow> quotesById = quoteRows.ToDictionary(quote => quote.Id);
        Dictionary<int, List<QuoteApprovalVoteInsightRow>> votesByApproval = approvalVotes
            .GroupBy(vote => vote.ApprovalId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return
        [
            .. approvalMessages
                .Where(approval => quotesById.ContainsKey(approval.QuoteId))
                .Select(approval =>
                {
                    QuoteInsightRow quote = quotesById[approval.QuoteId];
                    GuildQuoteConfigRow config = guildConfigById.GetValueOrDefault(quote.GuildId)
                        ?? GuildQuoteConfigRow.Unknown(quote.GuildId);
                    int requiredApprovals = Math.Max(
                        1,
                        approval.Type == QuoteApprovalType.AddRequest
                            ? config.AddRequiredApprovals
                            : config.RemoveRequiredApprovals);
                    List<QuoteApprovalVoteInsightRow> votes = votesByApproval.GetValueOrDefault(approval.Id) ?? [];
                    int currentApprovals = votes.Count;
                    DateTime expiresAt = approval.InsertedAtUtc.AddDays(approvalExpiryDays);
                    bool expired = !approval.Approved && expiresAt < now;
                    DateTime? completedAt = approval.Approved
                        ? votes.Count == 0 ? approval.InsertedAtUtc : votes.Max(vote => vote.InsertedAtUtc)
                        : null;
                    string status = approval.Approved ? "Approved" : expired ? "Expired" : "Pending";
                    return new DashboardQuoteApprovalRequestItem(
                        approval.Id,
                        approval.QuoteId,
                        quote.GuildId,
                        config.Name,
                        approval.Type == QuoteApprovalType.AddRequest ? "Add" : "Remove",
                        status,
                        currentApprovals,
                        requiredApprovals,
                        Math.Round(Math.Min(100.0, (double)currentApprovals / requiredApprovals * 100.0), 1),
                        approval.InsertedAtUtc,
                        completedAt,
                        expiresAt,
                        expired,
                        quote.Content,
                        quote.Username);
                })
        ];
    }

    private static IReadOnlyList<DashboardQuoteTimelinePoint> BuildQuoteTimeline(
        IReadOnlyList<QuoteInsightRow> quoteRows,
        IReadOnlyList<QuoteScoreInsightRow> scoreRows,
        IReadOnlyList<QuoteApprovalVoteInsightRow> approvalVotes,
        DateTime startDate,
        DateTime endExclusiveDate)
    {
        Dictionary<DateTime, List<QuoteInsightRow>> quotesByDate = quoteRows
            .GroupBy(quote => quote.InsertedAtUtc.Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        Dictionary<DateTime, List<QuoteScoreInsightRow>> scoresByDate = scoreRows
            .GroupBy(score => (score.UpdatedAtUtc ?? score.InsertedAtUtc).Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        Dictionary<DateTime, int> approvalVotesByDate = approvalVotes
            .GroupBy(vote => vote.InsertedAtUtc.Date)
            .ToDictionary(group => group.Key, group => group.Count());

        List<DashboardQuoteTimelinePoint> points = [];
        for (DateTime date = startDate.Date; date < endExclusiveDate.Date; date = date.AddDays(1))
        {
            List<QuoteInsightRow> quotes = quotesByDate.GetValueOrDefault(date) ?? [];
            List<QuoteScoreInsightRow> scores = scoresByDate.GetValueOrDefault(date) ?? [];
            points.Add(new DashboardQuoteTimelinePoint(
                date,
                quotes.Count,
                quotes.Count(quote => quote.Approved && !quote.Removed),
                quotes.Count(quote => !quote.Approved && !quote.Removed),
                quotes.Count(quote => quote.Removed),
                scores.Sum(score => score.Score),
                scores.Count,
                approvalVotesByDate.GetValueOrDefault(date)));
        }

        return points;
    }

    private static IReadOnlyList<DashboardQuoteTimelinePoint> BuildQuoteScoreTrend(
        IReadOnlyList<QuoteScoreInsightRow> scoreRows,
        DateTime startDate,
        DateTime endExclusiveDate)
    {
        Dictionary<DateTime, List<QuoteScoreInsightRow>> scoresByDate = scoreRows
            .GroupBy(score => (score.UpdatedAtUtc ?? score.InsertedAtUtc).Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        List<DashboardQuoteTimelinePoint> points = [];
        int cumulativeScore = 0;
        int cumulativeVotes = 0;

        for (DateTime date = startDate.Date; date < endExclusiveDate.Date; date = date.AddDays(1))
        {
            List<QuoteScoreInsightRow> scores = scoresByDate.GetValueOrDefault(date) ?? [];
            cumulativeScore += scores.Sum(score => score.Score);
            cumulativeVotes += scores.Count;
            points.Add(new DashboardQuoteTimelinePoint(
                date,
                scores.Count,
                0,
                0,
                0,
                cumulativeScore,
                cumulativeVotes,
                0));
        }

        return points;
    }

    private static IReadOnlyList<DashboardHistogramBucket> BuildApprovalTimeHistogram(IEnumerable<double> hours)
    {
        int underOne = 0;
        int oneToSix = 0;
        int sixToDay = 0;
        int oneToThreeDays = 0;
        int overThreeDays = 0;

        foreach (double value in hours)
        {
            if (value < 1.0)
                underOne++;
            else if (value < 6.0)
                oneToSix++;
            else if (value < 24.0)
                sixToDay++;
            else if (value < 72.0)
                oneToThreeDays++;
            else
                overThreeDays++;
        }

        return
        [
            new DashboardHistogramBucket("<1h", underOne),
            new DashboardHistogramBucket("1-6h", oneToSix),
            new DashboardHistogramBucket("6-24h", sixToDay),
            new DashboardHistogramBucket("1-3d", oneToThreeDays),
            new DashboardHistogramBucket("3d+", overThreeDays)
        ];
    }

    private static IReadOnlyList<DashboardCalendarActivityCell> BuildQuoteApprovalCalendar(
        IReadOnlyList<QuoteApprovalVoteInsightRow> approvalVotes,
        DateTime startDate,
        DateTime endExclusiveDate)
    {
        Dictionary<DateTime, List<QuoteApprovalVoteInsightRow>> votesByDate = approvalVotes
            .GroupBy(vote => vote.InsertedAtUtc.Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        List<DashboardCalendarActivityCell> cells = [];

        for (DateTime date = startDate.Date; date < endExclusiveDate.Date; date = date.AddDays(1))
        {
            List<QuoteApprovalVoteInsightRow> votes = votesByDate.GetValueOrDefault(date) ?? [];
            cells.Add(new DashboardCalendarActivityCell(
                date,
                votes.Count,
                votes.Count,
                votes.Select(vote => vote.UserId).Distinct().Count()));
        }

        return cells;
    }

    private static IReadOnlyList<DashboardQuoteServerSummary> BuildQuoteServerSummaries(
        IReadOnlyList<QuoteInsightRow> quoteRows,
        IReadOnlyList<DashboardQuoteApprovalRequestItem> approvalRequests,
        IReadOnlyDictionary<int, QuoteScoreStats> scoreStatsByQuote,
        IReadOnlyDictionary<int, GuildQuoteConfigRow> guildConfigById) =>
        [
            .. quoteRows
                .Select(quote => quote.GuildId)
                .Concat(approvalRequests.Select(request => request.GuildId))
                .Distinct()
                .Select(guildId =>
                {
                    GuildQuoteConfigRow config = guildConfigById.GetValueOrDefault(guildId)
                        ?? GuildQuoteConfigRow.Unknown(guildId);
                    List<QuoteInsightRow> rows = [.. quoteRows.Where(quote => quote.GuildId == guildId)];
                    List<DashboardQuoteApprovalRequestItem> serverApprovals =
                    [
                        .. approvalRequests.Where(request => request.GuildId == guildId)
                    ];
                    return new DashboardQuoteServerSummary(
                        guildId,
                        config.DiscordId,
                        config.Name,
                        rows.Count,
                        rows.Count(quote => quote.Approved && !quote.Removed),
                        rows.Count(quote => !quote.Approved && !quote.Removed),
                        rows.Count(quote => quote.Removed),
                        serverApprovals.Count,
                        serverApprovals.Count(request => request.Status == "Pending"),
                        rows.Sum(quote => scoreStatsByQuote.GetValueOrDefault(quote.Id)?.Total ?? 0),
                        rows.Sum(quote => scoreStatsByQuote.GetValueOrDefault(quote.Id)?.Votes ?? 0),
                        config.UsesGlobalQuotes,
                        config.ApprovalChannelConfigured,
                        GetQuoteSetupHealth(config).Health);
                })
                .OrderByDescending(summary => summary.Total)
                .ThenByDescending(summary => summary.ApprovalRequests)
                .ThenByDescending(summary => summary.TotalScore)
                .Take(12)
        ];

    private static IReadOnlyList<DashboardCategoryValue> BuildGlobalVsServerQuoteUsage(
        IReadOnlyList<QuoteInsightRow> quoteRows,
        IReadOnlyList<GuildQuoteConfigRow> guildConfigs)
    {
        HashSet<int> globalGuilds = [.. guildConfigs.Where(guild => guild.UsesGlobalQuotes).Select(guild => guild.GuildId)];
        int globalEnabledServers = globalGuilds.Count;
        int serverOnlyServers = Math.Max(0, guildConfigs.Count - globalEnabledServers);
        int quotesInGlobalEnabledServers = quoteRows.Count(quote => globalGuilds.Contains(quote.GuildId));
        int quotesInServerOnlyServers = quoteRows.Count - quotesInGlobalEnabledServers;

        return
        [
            new DashboardCategoryValue("Global-enabled servers", globalEnabledServers),
            new DashboardCategoryValue("Server-only servers", serverOnlyServers),
            new DashboardCategoryValue("Quotes in global-enabled servers", quotesInGlobalEnabledServers),
            new DashboardCategoryValue("Quotes in server-only servers", quotesInServerOnlyServers)
        ];
    }

    private static DashboardQuoteSetupSummary BuildQuoteSetupSummary(GuildQuoteConfigRow config)
    {
        (string health, string issue) = GetQuoteSetupHealth(config);
        return new DashboardQuoteSetupSummary(
            config.GuildId,
            config.DiscordId,
            config.Name,
            config.UsesGlobalQuotes,
            config.ApprovalChannelConfigured,
            config.AddRequiredApprovals,
            config.RemoveRequiredApprovals,
            health,
            issue);
    }

    private static (string Health, string Issue) GetQuoteSetupHealth(GuildQuoteConfigRow config)
    {
        if (!config.ApprovalChannelConfigured)
            return ("Missing", "No quote approval channel configured");
        if (config.AddRequiredApprovals <= 1 || config.RemoveRequiredApprovals <= 1)
            return ("Weak", "Approval threshold is low");
        return ("Healthy", config.UsesGlobalQuotes ? "Global quotes enabled" : "Server quote flow configured");
    }

    private static IReadOnlyList<DashboardQuoteRankedItem> BuildRankedQuotes(
        IReadOnlyList<QuoteInsightRow> quoteRows,
        IReadOnlyDictionary<int, QuoteScoreStats> scoreStatsByQuote,
        IReadOnlyDictionary<int, GuildQuoteConfigRow> guildConfigById) =>
        [
            .. quoteRows.Select(quote =>
            {
                QuoteScoreStats stats = scoreStatsByQuote.GetValueOrDefault(quote.Id) ?? QuoteScoreStats.Empty;
                GuildQuoteConfigRow config = guildConfigById.GetValueOrDefault(quote.GuildId)
                    ?? GuildQuoteConfigRow.Unknown(quote.GuildId);
                int controversy = Math.Min(stats.PositiveVotes, stats.NegativeVotes) * 2 + stats.Votes;
                return new DashboardQuoteRankedItem(
                    0,
                    quote.Id,
                    quote.GuildId,
                    config.Name,
                    quote.UserId,
                    quote.Username,
                    TrimForDashboard(quote.Content, 220),
                    quote.InsertedAtUtc,
                    quote.Approved,
                    quote.Removed,
                    stats.Total,
                    stats.PositiveVotes,
                    stats.NegativeVotes,
                    stats.Votes,
                    controversy);
            })
        ];

    private static IReadOnlyList<DashboardQuoteCandidate> BuildQuoteCandidates(
        string period,
        IReadOnlyList<QuoteInsightRow> quoteRows,
        IReadOnlyDictionary<int, QuoteScoreStats> scoreStatsByQuote,
        IReadOnlyDictionary<int, GuildQuoteConfigRow> guildConfigById,
        DateTime sinceUtc)
    {
        List<QuoteInsightRow> candidates =
        [
            .. quoteRows
                .Where(quote => quote.Approved && !quote.Removed && quote.InsertedAtUtc >= sinceUtc)
                .OrderByDescending(quote => scoreStatsByQuote.GetValueOrDefault(quote.Id)?.Total ?? 0)
                .ThenByDescending(quote => scoreStatsByQuote.GetValueOrDefault(quote.Id)?.Votes ?? 0)
                .Take(5)
        ];

        if (candidates.Count == 0)
        {
            candidates =
            [
                .. quoteRows
                    .Where(quote => quote.Approved && !quote.Removed)
                    .OrderByDescending(quote => scoreStatsByQuote.GetValueOrDefault(quote.Id)?.Total ?? 0)
                    .ThenByDescending(quote => scoreStatsByQuote.GetValueOrDefault(quote.Id)?.Votes ?? 0)
                    .Take(5)
            ];
        }

        return
        [
            .. candidates.Select((quote, index) =>
            {
                QuoteScoreStats stats = scoreStatsByQuote.GetValueOrDefault(quote.Id) ?? QuoteScoreStats.Empty;
                GuildQuoteConfigRow config = guildConfigById.GetValueOrDefault(quote.GuildId)
                    ?? GuildQuoteConfigRow.Unknown(quote.GuildId);
                return new DashboardQuoteCandidate(
                    index + 1,
                    period,
                    quote.Id,
                    quote.GuildId,
                    config.Name,
                    quote.Username,
                    TrimForDashboard(quote.Content, 180),
                    stats.Total,
                    stats.Votes,
                    quote.InsertedAtUtc);
            })
        ];
    }

    private static IReadOnlyList<DashboardQuoteVoteItem> BuildQuoteVoteItems(
        IReadOnlyList<QuoteScoreInsightRow> scoreRows) =>
        [
            .. scoreRows
                .GroupBy(score => score.UserId)
                .Select(group =>
                {
                    QuoteScoreInsightRow first = group.First();
                    List<QuoteScoreInsightRow> rows = [.. group];
                    return new DashboardQuoteVoteItem(
                        0,
                        first.UserId,
                        first.DiscordId,
                        first.Username,
                        rows.Count,
                        rows.Count(score => score.Score > 0),
                        rows.Count(score => score.Score < 0),
                        rows.Sum(score => score.Score),
                        rows.Max(score => score.UpdatedAtUtc ?? score.InsertedAtUtc));
                })
                .OrderByDescending(voter => voter.Votes)
                .ThenByDescending(voter => Math.Abs(voter.Score))
                .Take(20)
                .Select((voter, index) => voter with { Rank = index + 1 })
        ];

    private async Task<IReadOnlyList<DashboardQuoteVoteItem>> BuildQuoteApprovalVoteItemsAsync(
        IReadOnlyList<QuoteApprovalVoteInsightRow> approvalVotes,
        CancellationToken cancellationToken)
    {
        if (approvalVotes.Count == 0)
            return [];

        List<int> userIds =
        [
            .. approvalVotes
                .Select(vote => vote.UserId <= int.MaxValue ? (int)vote.UserId : 0)
                .Where(id => id > 0)
                .Distinct()
        ];
        Dictionary<int, (string DiscordId, string Username)> users = userIds.Count == 0
            ? []
            : await dbContext.Users
                .AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .ToDictionaryAsync(
                    user => user.Id,
                    user => (user.DiscordId.ToString(), user.Username),
                    cancellationToken);

        return
        [
            .. approvalVotes
                .GroupBy(vote => vote.UserId)
                .Select(group =>
                {
                    List<QuoteApprovalVoteInsightRow> rows = [.. group];
                    int intUserId = group.Key <= int.MaxValue ? (int)group.Key : 0;
                    (string discordId, string username) = users.GetValueOrDefault(
                        intUserId,
                        (string.Empty, intUserId > 0 ? $"User #{intUserId}" : group.Key.ToString()));
                    return new DashboardQuoteVoteItem(
                        0,
                        intUserId,
                        discordId,
                        username,
                        rows.Count,
                        rows.Count,
                        0,
                        rows.Count,
                        rows.Max(vote => vote.InsertedAtUtc));
                })
                .OrderByDescending(voter => voter.Votes)
                .ThenBy(voter => voter.Username)
                .Take(20)
                .Select((voter, index) => voter with { Rank = index + 1 })
        ];
    }

    private static IReadOnlyList<DashboardQuoteManagementItem> BuildQuoteManagementItems(
        IEnumerable<QuoteInsightRow> quoteRows,
        IReadOnlyDictionary<int, QuoteScoreStats> scoreStatsByQuote,
        IReadOnlyList<DashboardQuoteApprovalRequestItem> approvalRequests,
        IReadOnlyDictionary<int, GuildQuoteConfigRow> guildConfigById,
        bool includeRemoved,
        int limit)
    {
        Dictionary<int, int> pendingApprovalsByQuote = approvalRequests
            .Where(request => request.Status == "Pending")
            .GroupBy(request => request.QuoteId)
            .ToDictionary(group => group.Key, group => group.Count());

        return
        [
            .. quoteRows
                .Where(quote => includeRemoved || !quote.Removed)
                .OrderByDescending(quote => quote.InsertedAtUtc)
                .Take(limit)
                .Select(quote =>
                {
                    QuoteScoreStats stats = scoreStatsByQuote.GetValueOrDefault(quote.Id) ?? QuoteScoreStats.Empty;
                    GuildQuoteConfigRow config = guildConfigById.GetValueOrDefault(quote.GuildId)
                        ?? GuildQuoteConfigRow.Unknown(quote.GuildId);
                    return new DashboardQuoteManagementItem(
                        quote.Id,
                        quote.GuildId,
                        config.Name,
                        quote.UserId,
                        quote.DiscordId,
                        quote.Username,
                        TrimForDashboard(quote.Content, 260),
                        quote.InsertedAtUtc,
                        quote.Approved,
                        quote.Removed,
                        stats.Total,
                        stats.Votes,
                        pendingApprovalsByQuote.GetValueOrDefault(quote.Id),
                        stats.LastVotedAtUtc);
                })
        ];
    }

    private async Task<DashboardEconomyInsights> BuildEconomyInsightsAsync(
        IReadOnlyList<ScopedUserRow> scopedUsers,
        int? guildId,
        int? userId,
        bool restrictToScopedUsers,
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
        else if (restrictToScopedUsers)
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
                transaction.InsertDate,
                transaction.Id,
                transaction.StockId,
                transaction.Shares,
                transaction.PriceAtTransaction))
            .ToListAsync(cancellationToken);
        HashSet<int>? scopedUserIdSet = scopedUserIds.Count > 0
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
        decimal ubiPoolSize = await GetMoneySettingAmountAsync(UbiPoolSettingKey, 0m, cancellationToken);
        decimal slotsVaultSize = await GetMoneySettingAmountAsync(SlotsVaultSettingKey, SlotsVaultDefaultAmount, cancellationToken);
        decimal taxesCollected = transactions
            .Where(transaction => transaction.Type is TransactionType.StockSell or TransactionType.SlotsWin)
            .Sum(transaction => transaction.Fee);
        decimal ubiDonations = transactions
            .Where(transaction => transaction.Type == TransactionType.Donation)
            .Sum(transaction => Math.Abs(transaction.Amount));
        decimal transfersVolume = transactions
            .Where(transaction => transaction.Type is TransactionType.Transfer or TransactionType.StockTransfer)
            .Sum(transaction => Math.Abs(transaction.Amount));
        decimal userToUserTransferVolume = transactions
            .Where(transaction => transaction.Type == TransactionType.Transfer)
            .Sum(transaction => Math.Abs(transaction.Amount));
        decimal wealthTaxImpact = scopedUsers
            .Where(user => user.Balance > 0m)
            .Sum(user => user.Balance * 0.0001m);
        int robberyWins = transactions.Count(transaction => transaction.Type == TransactionType.RobberyWin);
        int robberyLosses = transactions.Count(transaction => transaction.Type == TransactionType.RobberyLoss);
        int robberyAttempts = robberyWins + robberyLosses;
        int slotsWins = transactions.Count(transaction => transaction.Type == TransactionType.SlotsWin);
        int slotsLosses = transactions.Count(transaction => transaction.Type == TransactionType.SlotsLoss);
        decimal slotsWinVolume = transactions
            .Where(transaction => transaction.Type == TransactionType.SlotsWin)
            .Sum(transaction => Math.Abs(transaction.Amount));
        decimal slotsLossVolume = transactions
            .Where(transaction => transaction.Type == TransactionType.SlotsLoss)
            .Sum(transaction => Math.Abs(transaction.Amount));

        IReadOnlyList<DashboardEconomyFlowPoint> dailyFlow = BuildEconomyDailyFlow(scopedTransactions, startDate, days);
        decimal inflows = dailyFlow.Sum(point => point.Inflow);
        decimal outflows = dailyFlow.Sum(point => point.Outflow);
        IReadOnlyList<DashboardEconomySupplyPoint> moneySupplyTrend = BuildMoneySupplyTrend(
            dailyFlow,
            cashBalance,
            ubiPoolSize,
            slotsVaultSize);
        IReadOnlyList<DashboardEconomyFlowPoint> ubiPoolTrend = BuildPoolTrend(
            transactions,
            startDate,
            days,
            ubiPoolSize,
            transaction => transaction.Type == TransactionType.Donation
                ? Math.Abs(transaction.Amount)
                : transaction.Fee);
        IReadOnlyList<DashboardEconomyFlowPoint> slotsVaultTrend = BuildPoolTrend(
            transactions.Where(transaction => transaction.Type is TransactionType.SlotsWin or TransactionType.SlotsLoss),
            startDate,
            days,
            slotsVaultSize,
            transaction => transaction.Type == TransactionType.SlotsLoss ? Math.Abs(transaction.Amount) : -Math.Abs(transaction.Amount));
        IReadOnlyList<DashboardEconomyFlowPoint> slotsProfitLoss = BuildSlotsProfitLoss(
            transactions,
            startDate,
            days);
        IReadOnlyList<DashboardEconomyStackedPoint> transactionVolumeTimeline = BuildEconomyStackedTimeline(
            transactions,
            startDate,
            days);
        IReadOnlyList<DashboardHistogramBucket> balanceDistribution = BuildBalanceDistribution(
            scopedUsers.Select(user => user.Balance));
        IReadOnlyList<DashboardCategoryValue> wealthInequality = BuildWealthInequality(scopedUsers, portfolios);
        IReadOnlyList<DashboardEconomyHeatmapCell> economyHeatmap = BuildEconomyHeatmap(transactions);
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
        IReadOnlyList<DashboardWealthUser> cashLeaders =
        [
            .. scopedUsers
                .OrderByDescending(user => user.Balance)
                .Take(10)
                .Select((user, index) => new DashboardWealthUser(
                    index + 1,
                    user.UserId,
                    user.DiscordId,
                    user.Username,
                    user.Balance,
                    portfolios.GetValueOrDefault(user.UserId),
                    user.Balance + portfolios.GetValueOrDefault(user.UserId)))
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
        IReadOnlyList<DashboardEconomyActor> topDonors = await BuildEconomyActorRowsAsync(
            transactions.Where(transaction => transaction.Type == TransactionType.Donation),
            actorSelector: transaction => transaction.UserId,
            amountSelector: transaction => Math.Abs(transaction.Amount),
            countLabel: "donations",
            cancellationToken: cancellationToken);
        IReadOnlyList<DashboardEconomyActor> biggestRobberies = await BuildEconomyActorRowsAsync(
            transactions.Where(transaction => transaction.Type == TransactionType.RobberyWin),
            actorSelector: transaction => transaction.UserId,
            amountSelector: transaction => Math.Abs(transaction.Amount),
            countLabel: "robberies",
            cancellationToken: cancellationToken,
            individualRows: true);
        IReadOnlyList<DashboardEconomyActor> mostRobbedUsers = await BuildEconomyActorRowsAsync(
            transactions.Where(transaction => transaction.Type == TransactionType.RobberyWin && transaction.TargetUserId.HasValue),
            actorSelector: transaction => transaction.TargetUserId!.Value,
            amountSelector: transaction => Math.Abs(transaction.Amount),
            countLabel: "times robbed",
            cancellationToken: cancellationToken);
        IReadOnlyList<DashboardEconomyActor> mostSuccessfulRobbers = await BuildEconomyActorRowsAsync(
            transactions.Where(transaction => transaction.Type == TransactionType.RobberyWin),
            actorSelector: transaction => transaction.UserId,
            amountSelector: transaction => Math.Abs(transaction.Amount),
            countLabel: "successful robberies",
            cancellationToken: cancellationToken);
        IReadOnlyList<DashboardEconomyActor> biggestSlotsWins = await BuildEconomyActorRowsAsync(
            transactions.Where(transaction => transaction.Type == TransactionType.SlotsWin),
            actorSelector: transaction => transaction.UserId,
            amountSelector: transaction => Math.Abs(transaction.Amount),
            countLabel: "wins",
            cancellationToken: cancellationToken,
            individualRows: true);
        IReadOnlyList<DashboardEconomyActor> biggestSlotsLosses = await BuildEconomyActorRowsAsync(
            transactions.Where(transaction => transaction.Type == TransactionType.SlotsLoss),
            actorSelector: transaction => transaction.UserId,
            amountSelector: transaction => Math.Abs(transaction.Amount),
            countLabel: "losses",
            cancellationToken: cancellationToken,
            individualRows: true);
        IReadOnlyList<DashboardCategoryValue> robberyOutcomes =
        [
            new DashboardCategoryValue("Wins", robberyWins),
            new DashboardCategoryValue("Losses", robberyLosses)
        ];
        IReadOnlyList<DashboardCategoryValue> slotsOutcomes =
        [
            new DashboardCategoryValue("Wins", slotsWins),
            new DashboardCategoryValue("Losses", slotsLosses)
        ];
        List<decimal> balances = [.. scopedUsers.Select(user => user.Balance).OrderBy(balance => balance)];

        return new DashboardEconomyInsights(
            cashBalance + ubiPoolSize + slotsVaultSize,
            cashBalance,
            portfolioValue,
            cashBalance + portfolioValue,
            scopedUsers.Count == 0 ? 0m : Math.Round(cashBalance / scopedUsers.Count, 2),
            Median(balances),
            scopedUsers.Count(user => user.Balance != 0m || portfolios.GetValueOrDefault(user.UserId) != 0m),
            CountActiveTransactionParticipants(transactions, scopedUserIdSet),
            transactionVolume,
            transactions.Count,
            fees,
            taxesCollected,
            ubiPoolSize,
            ubiDonations,
            wealthTaxImpact,
            transfersVolume,
            userToUserTransferVolume,
            inflows,
            outflows,
            robberyWins,
            robberyLosses,
            robberyAttempts == 0 ? 0.0 : Math.Round((double)robberyWins / robberyAttempts * 100.0, 1),
            slotsWins,
            slotsLosses,
            slotsVaultSize,
            slotsLossVolume == 0m ? 0.0 : Math.Round((double)(slotsWinVolume / slotsLossVolume), 2),
            moneySupplyTrend,
            dailyFlow,
            ubiPoolTrend,
            slotsVaultTrend,
            slotsProfitLoss,
            transactionVolumeTimeline,
            transactionTypes,
            moneyFlows,
            cashLeaders,
            wealthLeaders,
            balanceDistribution,
            wealthInequality,
            topDonors,
            biggestRobberies,
            mostRobbedUsers,
            mostSuccessfulRobbers,
            robberyOutcomes,
            biggestSlotsWins,
            biggestSlotsLosses,
            slotsOutcomes,
            economyHeatmap);
    }

    private static decimal Median(IReadOnlyList<decimal> sortedValues)
    {
        if (sortedValues.Count == 0)
            return 0m;

        int middle = sortedValues.Count / 2;
        return sortedValues.Count % 2 == 1
            ? sortedValues[middle]
            : Math.Round((sortedValues[middle - 1] + sortedValues[middle]) / 2m, 2);
    }

    private static IReadOnlyList<DashboardEconomySupplyPoint> BuildMoneySupplyTrend(
        IReadOnlyList<DashboardEconomyFlowPoint> dailyFlow,
        decimal currentCashBalance,
        decimal currentUbiPool,
        decimal currentSlotsVault)
    {
        decimal futureNet = dailyFlow.Sum(point => point.Net);
        decimal runningCash = currentCashBalance - futureNet;
        List<DashboardEconomySupplyPoint> points = [];

        foreach (DashboardEconomyFlowPoint point in dailyFlow)
        {
            runningCash += point.Net;
            points.Add(new DashboardEconomySupplyPoint(
                point.DateUtc,
                runningCash + currentUbiPool + currentSlotsVault,
                runningCash,
                currentUbiPool,
                currentSlotsVault,
                point.Inflow,
                point.Outflow));
        }

        return points;
    }

    private static IReadOnlyList<DashboardEconomyFlowPoint> BuildPoolTrend(
        IEnumerable<TransactionInsightRow> transactions,
        DateTime startDate,
        int days,
        decimal currentBalance,
        Func<TransactionInsightRow, decimal> contributionSelector)
    {
        Dictionary<DateTime, List<TransactionInsightRow>> byDate = transactions
            .GroupBy(transaction => transaction.InsertDate.Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        List<decimal> dailyContributions = [];
        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            dailyContributions.Add(byDate.GetValueOrDefault(date, []).Sum(contributionSelector));
        }

        decimal running = currentBalance - dailyContributions.Sum();
        List<DashboardEconomyFlowPoint> points = [];
        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            decimal contribution = dailyContributions[offset];
            running += contribution;
            points.Add(new DashboardEconomyFlowPoint(
                DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                contribution > 0m ? contribution : 0m,
                contribution < 0m ? Math.Abs(contribution) : 0m,
                running));
        }

        return points;
    }

    private static IReadOnlyList<DashboardEconomyFlowPoint> BuildSlotsProfitLoss(
        IEnumerable<TransactionInsightRow> transactions,
        DateTime startDate,
        int days)
    {
        Dictionary<DateTime, List<TransactionInsightRow>> byDate = transactions
            .Where(transaction => transaction.Type is TransactionType.SlotsWin or TransactionType.SlotsLoss)
            .GroupBy(transaction => transaction.InsertDate.Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        List<DashboardEconomyFlowPoint> points = [];

        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            List<TransactionInsightRow> rows = byDate.GetValueOrDefault(date, []);
            decimal wins = rows
                .Where(transaction => transaction.Type == TransactionType.SlotsWin)
                .Sum(transaction => Math.Abs(transaction.Amount));
            decimal losses = rows
                .Where(transaction => transaction.Type == TransactionType.SlotsLoss)
                .Sum(transaction => Math.Abs(transaction.Amount));
            points.Add(new DashboardEconomyFlowPoint(
                DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                wins,
                losses,
                wins - losses));
        }

        return points;
    }

    private static IReadOnlyList<DashboardEconomyStackedPoint> BuildEconomyStackedTimeline(
        IReadOnlyList<TransactionInsightRow> transactions,
        DateTime startDate,
        int days)
    {
        Dictionary<DateTime, List<TransactionInsightRow>> byDate = transactions
            .GroupBy(transaction => transaction.InsertDate.Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        List<DashboardEconomyStackedPoint> points = [];

        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            List<TransactionInsightRow> rows = byDate.GetValueOrDefault(date, []);
            points.Add(new DashboardEconomyStackedPoint(
                DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                SumTransactionType(rows, TransactionType.StockBuy),
                SumTransactionType(rows, TransactionType.StockSell),
                SumTransactionType(rows, TransactionType.Transfer),
                SumTransactionType(rows, TransactionType.Donation),
                SumTransactionType(rows, TransactionType.SlotsWin),
                SumTransactionType(rows, TransactionType.SlotsLoss),
                SumTransactionType(rows, TransactionType.RobberyWin),
                SumTransactionType(rows, TransactionType.RobberyLoss),
                SumTransactionType(rows, TransactionType.StockTransfer),
                rows.Sum(row => row.Fee),
                rows.Where(row => row.Type is TransactionType.StockSell or TransactionType.SlotsWin).Sum(row => row.Fee)));
        }

        return points;

        static decimal SumTransactionType(IEnumerable<TransactionInsightRow> rows, TransactionType type) =>
            rows.Where(row => row.Type == type).Sum(row => Math.Abs(row.Amount));
    }

    private static IReadOnlyList<DashboardHistogramBucket> BuildBalanceDistribution(IEnumerable<decimal> balances)
    {
        int negative = 0;
        int zero = 0;
        int low = 0;
        int medium = 0;
        int high = 0;
        int whale = 0;

        foreach (decimal balance in balances)
        {
            if (balance < 0m)
                negative++;
            else if (balance == 0m)
                zero++;
            else if (balance < 1_000m)
                low++;
            else if (balance < 10_000m)
                medium++;
            else if (balance < 100_000m)
                high++;
            else
                whale++;
        }

        return
        [
            new DashboardHistogramBucket("<0", negative),
            new DashboardHistogramBucket("0", zero),
            new DashboardHistogramBucket("1-999", low),
            new DashboardHistogramBucket("1k-10k", medium),
            new DashboardHistogramBucket("10k-100k", high),
            new DashboardHistogramBucket("100k+", whale)
        ];
    }

    private static IReadOnlyList<DashboardCategoryValue> BuildWealthInequality(
        IReadOnlyList<ScopedUserRow> users,
        IReadOnlyDictionary<int, decimal> portfolios)
    {
        List<decimal> netWorths =
        [
            .. users
                .Select(user => Math.Max(0m, user.Balance + portfolios.GetValueOrDefault(user.UserId)))
                .OrderByDescending(value => value)
        ];
        decimal total = netWorths.Sum();
        if (netWorths.Count == 0 || total == 0m)
            return [];

        decimal topOne = netWorths.Take(Math.Max(1, (int)Math.Ceiling(netWorths.Count * 0.01m))).Sum();
        decimal topFive = netWorths.Take(Math.Max(1, (int)Math.Ceiling(netWorths.Count * 0.05m))).Sum();
        decimal topTen = netWorths.Take(Math.Max(1, (int)Math.Ceiling(netWorths.Count * 0.10m))).Sum();
        decimal gini = CalculateGini([.. netWorths.OrderBy(value => value)]);

        return
        [
            new DashboardCategoryValue("Top 1% share", Math.Round(topOne / total * 100m, 1)),
            new DashboardCategoryValue("Top 5% share", Math.Round(topFive / total * 100m, 1)),
            new DashboardCategoryValue("Top 10% share", Math.Round(topTen / total * 100m, 1)),
            new DashboardCategoryValue("Gini estimate", Math.Round(gini * 100m, 1))
        ];
    }

    private static decimal CalculateGini(IReadOnlyList<decimal> sortedValues)
    {
        if (sortedValues.Count == 0)
            return 0m;

        decimal total = sortedValues.Sum();
        if (total == 0m)
            return 0m;

        decimal weighted = 0m;
        for (int index = 0; index < sortedValues.Count; index++)
            weighted += (index + 1) * sortedValues[index];

        return (2m * weighted) / (sortedValues.Count * total) - ((decimal)sortedValues.Count + 1m) / sortedValues.Count;
    }

    private async Task<IReadOnlyList<DashboardEconomyActor>> BuildEconomyActorRowsAsync(
        IEnumerable<TransactionInsightRow> transactions,
        Func<TransactionInsightRow, int> actorSelector,
        Func<TransactionInsightRow, decimal> amountSelector,
        string countLabel,
        CancellationToken cancellationToken,
        bool individualRows = false)
    {
        List<TransactionInsightRow> rows = [.. transactions];
        if (rows.Count == 0)
            return [];

        List<int> userIds = [.. rows.Select(actorSelector).Distinct()];
        Dictionary<int, (string DiscordId, string Username)> labels = await GetUserLabelsAsync(userIds, cancellationToken);

        if (individualRows)
        {
            return
            [
                .. rows
                    .OrderByDescending(amountSelector)
                    .ThenByDescending(transaction => transaction.InsertDate)
                    .Take(8)
                    .Select((transaction, index) =>
                    {
                        int userId = actorSelector(transaction);
                        (string discordId, string username) = labels.GetValueOrDefault(userId, (string.Empty, $"User #{userId}"));
                        return new DashboardEconomyActor(
                            index + 1,
                            userId,
                            discordId,
                            username,
                            amountSelector(transaction),
                            1,
                            transaction.Fee,
                            TransactionTypeLabel(transaction.Type));
                    })
            ];
        }

        return
        [
            .. rows
                .GroupBy(actorSelector)
                .Select(group =>
                {
                    List<TransactionInsightRow> groupRows = [.. group];
                    (string discordId, string username) = labels.GetValueOrDefault(group.Key, (string.Empty, $"User #{group.Key}"));
                    return new DashboardEconomyActor(
                        0,
                        group.Key,
                        discordId,
                        username,
                        groupRows.Sum(amountSelector),
                        groupRows.Count,
                        groupRows.Max(amountSelector),
                        countLabel);
                })
                .OrderByDescending(actor => actor.Amount)
                .ThenByDescending(actor => actor.Count)
                .Take(10)
                .Select((actor, index) => actor with { Rank = index + 1 })
        ];
    }

    private static IReadOnlyList<DashboardEconomyHeatmapCell> BuildEconomyHeatmap(
        IReadOnlyList<TransactionInsightRow> transactions)
    {
        Dictionary<(int Day, int Hour), List<TransactionInsightRow>> grouped = transactions
            .GroupBy(transaction => ((int)transaction.InsertDate.DayOfWeek, transaction.InsertDate.Hour))
            .ToDictionary(group => group.Key, group => group.ToList());
        List<DashboardEconomyHeatmapCell> cells = [];

        for (int day = 0; day < 7; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                List<TransactionInsightRow> rows = grouped.GetValueOrDefault((day, hour), []);
                cells.Add(new DashboardEconomyHeatmapCell(
                    day,
                    DayLabels[day],
                    hour,
                    rows.Count,
                    rows.Sum(row => Math.Abs(row.Amount)),
                    rows.Select(row => row.UserId).Concat(rows.Where(row => row.TargetUserId.HasValue).Select(row => row.TargetUserId!.Value)).Distinct().Count()));
            }
        }

        return cells;
    }

    private async Task<DashboardStockMarketInsights> BuildStockMarketInsightsAsync(
        int? guildId,
        int? userId,
        ulong? channelDiscordId,
        IReadOnlyList<int> scopedUserIds,
        IQueryable<UserActivity> activityQuery,
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        CancellationToken cancellationToken)
    {
        HashSet<int> scopedUserSet = [.. scopedUserIds];
        HashSet<int> scopedChannelEntityIds = await GetScopedChannelEntityIdsAsync(
            guildId,
            channelDiscordId,
            activityQuery,
            cancellationToken);

        IQueryable<Stock> stockQuery = dbContext.Stocks.AsNoTracking();
        if (channelDiscordId.HasValue)
        {
            stockQuery = stockQuery.Where(stock =>
                stock.EntityType == StockEntityType.Channel &&
                scopedChannelEntityIds.Contains(stock.EntityId));
        }
        else if (userId.HasValue)
        {
            stockQuery = stockQuery.Where(stock =>
                stock.EntityType == StockEntityType.User &&
                stock.EntityId == userId.Value);
        }
        else if (guildId.HasValue)
        {
            stockQuery = stockQuery.Where(stock =>
                stock.EntityType == StockEntityType.Guild && stock.EntityId == guildId.Value ||
                stock.EntityType == StockEntityType.User && scopedUserSet.Contains(stock.EntityId) ||
                stock.EntityType == StockEntityType.Channel && scopedChannelEntityIds.Contains(stock.EntityId));
        }

        List<StockInsightRow> stockRows = await stockQuery
            .Select(stock => new StockInsightRow(
                stock.Id,
                stock.EntityType,
                stock.EntityId,
                stock.Price,
                stock.DailyChangePercent,
                stock.PreviousPrice,
                stock.InsertDate,
                stock.LastUpdatedDate))
            .ToListAsync(cancellationToken);
        List<int> stockIds = [.. stockRows.Select(stock => stock.StockId)];
        List<StockHoldingInsightRow> holdings = await GetStockHoldingRowsAsync(stockIds, cancellationToken);
        Dictionary<int, decimal> holdingValues = holdings
            .GroupBy(holding => holding.StockId)
            .ToDictionary(group => group.Key, group => group.Sum(holding => holding.Value));
        Dictionary<int, decimal> sharesByStock = holdings
            .GroupBy(holding => holding.StockId)
            .ToDictionary(group => group.Key, group => group.Sum(holding => holding.Shares));
        Dictionary<int, int> holdersByStock = holdings
            .GroupBy(holding => holding.StockId)
            .ToDictionary(group => group.Key, group => group.Select(holding => holding.UserId).Distinct().Count());
        Dictionary<int, string> stockNames = await GetStockNamesAsync(stockRows, cancellationToken);
        List<TransactionInsightRow> stockTransactions = stockIds.Count == 0
            ? []
            : await dbContext.StockTransactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.StockId.HasValue &&
                    stockIds.Contains(transaction.StockId.Value) &&
                    transaction.InsertDate >= startDate &&
                    transaction.InsertDate < endExclusiveDate)
                .Select(transaction => new TransactionInsightRow(
                    transaction.UserId,
                    transaction.TargetUserId,
                    transaction.Type,
                    transaction.Amount,
                    transaction.Fee,
                    transaction.InsertDate,
                    transaction.Id,
                    transaction.StockId,
                    transaction.Shares,
                    transaction.PriceAtTransaction))
                .ToListAsync(cancellationToken);
        Dictionary<int, decimal> tradeVolumeByStock = stockTransactions
            .Where(transaction => transaction.StockId.HasValue)
            .GroupBy(transaction => transaction.StockId!.Value)
            .ToDictionary(group => group.Key, group => group.Sum(transaction => Math.Abs(transaction.Amount)));
        Dictionary<int, int> tradeCountByStock = stockTransactions
            .Where(transaction => transaction.StockId.HasValue)
            .GroupBy(transaction => transaction.StockId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

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
        IReadOnlyList<DashboardStockTableItem> stockTableItems = BuildStockTableItems(
            stockRows,
            stockNames,
            holdingValues,
            sharesByStock,
            holdersByStock,
            tradeVolumeByStock,
            tradeCountByStock);
        IReadOnlyList<DashboardStockHoldingSummary> holdingsByUser = BuildStockHoldingsByUser(holdings);
        IReadOnlyList<DashboardStockHoldingItem> holdingsTable = BuildStockHoldingItems(holdings, stockNames);
        IReadOnlyList<DashboardStockTradePoint> tradeVolumeTimeline = BuildStockTradeTimeline(
            stockTransactions,
            startDate,
            days);
        decimal buyVolume = stockTransactions
            .Where(transaction => transaction.Type == TransactionType.StockBuy)
            .Sum(transaction => Math.Abs(transaction.Amount));
        decimal sellVolume = stockTransactions
            .Where(transaction => transaction.Type == TransactionType.StockSell)
            .Sum(transaction => Math.Abs(transaction.Amount));
        decimal transferVolume = stockTransactions
            .Where(transaction => transaction.Type == TransactionType.StockTransfer)
            .Sum(transaction => Math.Abs(transaction.Amount));
        IReadOnlyList<DashboardStockActivityPricePoint> activityToPrice = await BuildStockActivityToPriceAsync(
            stockRows,
            stockNames,
            holdingValues,
            activityQuery,
            cancellationToken);
        IReadOnlyList<DashboardCategoryValue> ownershipConcentration = BuildOwnershipConcentration(holdings);

        return new DashboardStockMarketInsights(
            Stocks: stockRows.Count,
            UserStocks: stockRows.Count(stock => stock.EntityType == StockEntityType.User),
            ServerStocks: stockRows.Count(stock => stock.EntityType == StockEntityType.Guild),
            ChannelStocks: stockRows.Count(stock => stock.EntityType == StockEntityType.Channel),
            MarketValue: holdingValues.Values.Sum(),
            AveragePrice: stockRows.Count == 0 ? 0m : Math.Round(stockRows.Average(stock => stock.Price), 2),
            AverageDailyChangePercent: stockRows.Count == 0 ? 0.0 : Math.Round((double)stockRows.Average(stock => stock.DailyChangePercent), 2),
            BuyVolume: buyVolume,
            SellVolume: sellVolume,
            StockTransferVolume: transferVolume,
            Winners: [.. movers.OrderByDescending(stock => stock.DailyChangePercent).Take(5)],
            Losers: [.. movers.OrderBy(stock => stock.DailyChangePercent).Take(5)],
            EntityTypes: entityTypes,
            MostValuableStocks: [.. stockTableItems.OrderByDescending(stock => stock.HoldingValue).Take(10).Select((stock, index) => stock with { Rank = index + 1 })],
            MostHeldStocks: [.. stockTableItems.OrderByDescending(stock => stock.Holders).ThenByDescending(stock => stock.SharesHeld).Take(10).Select((stock, index) => stock with { Rank = index + 1 })],
            MostTradedStocks: [.. stockTableItems.OrderByDescending(stock => stock.TradeVolume).ThenByDescending(stock => stock.TradeCount).Take(10).Select((stock, index) => stock with { Rank = index + 1 })],
            NewestStocks: [.. stockTableItems.OrderByDescending(stock => stock.InsertedAtUtc).Take(10).Select((stock, index) => stock with { Rank = index + 1 })],
            DailyChangeHistogram: BuildDailyChangeHistogram(stockRows.Select(stock => stock.DailyChangePercent)),
            PriceMovement: [.. stockRows
                .OrderByDescending(stock => Math.Abs(stock.DailyChangePercent))
                .Take(10)
                .Select(stock => new DashboardCategoryValue(
                    stockNames.GetValueOrDefault(stock.StockId, $"Stock #{stock.StockId}"),
                    stock.DailyChangePercent))],
            HoldingsByUser: holdingsByUser,
            HoldingsTable: holdingsTable,
            TradeVolumeTimeline: tradeVolumeTimeline,
            BuyVsSell: [
                new DashboardCategoryValue("Buy volume", buyVolume),
                new DashboardCategoryValue("Sell volume", sellVolume),
                new DashboardCategoryValue("Transfer activity", transferVolume)
            ],
            OwnershipConcentration: ownershipConcentration,
            ActivityToPrice: activityToPrice);
    }

    private async Task<List<StockHoldingInsightRow>> GetStockHoldingRowsAsync(
        IReadOnlyList<int> stockIds,
        CancellationToken cancellationToken)
    {
        if (stockIds.Count == 0)
            return [];

        return await dbContext.StockHoldings
            .AsNoTracking()
            .Where(holding => stockIds.Contains(holding.StockId))
            .Join(
                dbContext.Stocks.AsNoTracking(),
                holding => holding.StockId,
                stock => stock.Id,
                (holding, stock) => new { holding, stock })
            .Join(
                dbContext.Users.AsNoTracking(),
                row => row.holding.UserId,
                user => user.Id,
                (row, user) => new StockHoldingInsightRow(
                    row.holding.UserId,
                    user.DiscordId.ToString(),
                    user.Username,
                    row.holding.StockId,
                    row.holding.Shares,
                    row.stock.Price,
                    row.holding.Shares * row.stock.Price,
                    row.holding.TotalInvested,
                    EntityTypeLabel(row.stock.EntityType)))
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyList<DashboardStockTableItem> BuildStockTableItems(
        IReadOnlyList<StockInsightRow> stocks,
        IReadOnlyDictionary<int, string> names,
        IReadOnlyDictionary<int, decimal> holdingValues,
        IReadOnlyDictionary<int, decimal> sharesByStock,
        IReadOnlyDictionary<int, int> holdersByStock,
        IReadOnlyDictionary<int, decimal> tradeVolumeByStock,
        IReadOnlyDictionary<int, int> tradeCountByStock) =>
        [
            .. stocks.Select((stock, index) => new DashboardStockTableItem(
                index + 1,
                stock.StockId,
                EntityTypeLabel(stock.EntityType),
                stock.EntityId,
                names.GetValueOrDefault(stock.StockId, $"Stock #{stock.StockId}"),
                stock.Price,
                stock.PreviousPrice == 0m ? stock.Price : stock.PreviousPrice,
                stock.DailyChangePercent,
                holdingValues.GetValueOrDefault(stock.StockId),
                sharesByStock.GetValueOrDefault(stock.StockId),
                holdersByStock.GetValueOrDefault(stock.StockId),
                tradeVolumeByStock.GetValueOrDefault(stock.StockId),
                tradeCountByStock.GetValueOrDefault(stock.StockId),
                stock.InsertedAtUtc == default
                    ? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
                    : stock.InsertedAtUtc))
        ];

    private static IReadOnlyList<DashboardStockHoldingSummary> BuildStockHoldingsByUser(
        IReadOnlyList<StockHoldingInsightRow> holdings)
    {
        decimal totalValue = holdings.Sum(holding => holding.Value);
        return
        [
            .. holdings
                .GroupBy(holding => holding.UserId)
                .Select(group =>
                {
                    List<StockHoldingInsightRow> rows = [.. group];
                    StockHoldingInsightRow first = rows[0];
                    decimal value = rows.Sum(row => row.Value);
                    return new DashboardStockHoldingSummary(
                        0,
                        first.UserId,
                        first.DiscordId,
                        first.Username,
                        value,
                        rows.Sum(row => row.Shares),
                        rows.Count,
                        totalValue == 0m ? 0m : Math.Round(value / totalValue * 100m, 1));
                })
                .OrderByDescending(summary => summary.PortfolioValue)
                .Take(12)
                .Select((summary, index) => summary with { Rank = index + 1 })
        ];
    }

    private static IReadOnlyList<DashboardStockHoldingItem> BuildStockHoldingItems(
        IReadOnlyList<StockHoldingInsightRow> holdings,
        IReadOnlyDictionary<int, string> stockNames)
    {
        decimal totalValue = holdings.Sum(holding => holding.Value);
        return
        [
            .. holdings
                .OrderByDescending(holding => holding.Value)
                .Take(20)
                .Select((holding, index) => new DashboardStockHoldingItem(
                    index + 1,
                    holding.UserId,
                    holding.DiscordId,
                    holding.Username,
                    holding.StockId,
                    stockNames.GetValueOrDefault(holding.StockId, $"Stock #{holding.StockId}"),
                    holding.EntityType,
                    holding.Shares,
                    holding.Price,
                    holding.Value,
                    totalValue == 0m ? 0m : Math.Round(holding.Value / totalValue * 100m, 1),
                    holding.Value - holding.TotalInvested))
        ];
    }

    private static IReadOnlyList<DashboardStockTradePoint> BuildStockTradeTimeline(
        IReadOnlyList<TransactionInsightRow> transactions,
        DateTime startDate,
        int days)
    {
        Dictionary<DateTime, List<TransactionInsightRow>> byDate = transactions
            .GroupBy(transaction => transaction.InsertDate.Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        List<DashboardStockTradePoint> points = [];

        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            List<TransactionInsightRow> rows = byDate.GetValueOrDefault(date, []);
            points.Add(new DashboardStockTradePoint(
                DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                rows.Where(transaction => transaction.Type == TransactionType.StockBuy).Sum(transaction => Math.Abs(transaction.Amount)),
                rows.Where(transaction => transaction.Type == TransactionType.StockSell).Sum(transaction => Math.Abs(transaction.Amount)),
                rows.Where(transaction => transaction.Type == TransactionType.StockTransfer).Sum(transaction => Math.Abs(transaction.Amount)),
                rows.Count));
        }

        return points;
    }

    private static IReadOnlyList<DashboardHistogramBucket> BuildDailyChangeHistogram(IEnumerable<decimal> changes)
    {
        int hardDown = 0;
        int down = 0;
        int flatDown = 0;
        int flat = 0;
        int flatUp = 0;
        int up = 0;
        int hardUp = 0;

        foreach (decimal change in changes)
        {
            if (change <= -10m)
                hardDown++;
            else if (change <= -3m)
                down++;
            else if (change < 0m)
                flatDown++;
            else if (change == 0m)
                flat++;
            else if (change < 3m)
                flatUp++;
            else if (change < 10m)
                up++;
            else
                hardUp++;
        }

        return
        [
            new DashboardHistogramBucket("<= -10%", hardDown),
            new DashboardHistogramBucket("-10 to -3%", down),
            new DashboardHistogramBucket("-3 to 0%", flatDown),
            new DashboardHistogramBucket("0%", flat),
            new DashboardHistogramBucket("0 to 3%", flatUp),
            new DashboardHistogramBucket("3 to 10%", up),
            new DashboardHistogramBucket("10%+", hardUp)
        ];
    }

    private static IReadOnlyList<DashboardCategoryValue> BuildOwnershipConcentration(
        IReadOnlyList<StockHoldingInsightRow> holdings)
    {
        List<decimal> values = [.. holdings.Select(holding => holding.Value).OrderByDescending(value => value)];
        decimal total = values.Sum();
        if (values.Count == 0 || total == 0m)
            return [];

        decimal topOne = values.Take(1).Sum();
        decimal topThree = values.Take(3).Sum();
        decimal topTen = values.Take(10).Sum();

        return
        [
            new DashboardCategoryValue("Top holder", Math.Round(topOne / total * 100m, 1)),
            new DashboardCategoryValue("Top 3 holders", Math.Round(topThree / total * 100m, 1)),
            new DashboardCategoryValue("Top 10 holders", Math.Round(topTen / total * 100m, 1)),
            new DashboardCategoryValue("Remaining holders", Math.Round(Math.Max(0m, total - topTen) / total * 100m, 1))
        ];
    }

    private async Task<IReadOnlyList<DashboardStockActivityPricePoint>> BuildStockActivityToPriceAsync(
        IReadOnlyList<StockInsightRow> stocks,
        IReadOnlyDictionary<int, string> names,
        IReadOnlyDictionary<int, decimal> holdingValues,
        IQueryable<UserActivity> activityQuery,
        CancellationToken cancellationToken)
    {
        if (stocks.Count == 0)
            return [];

        var userActivity = await activityQuery
            .GroupBy(activity => activity.UserId)
            .Select(group => new
            {
                EntityId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .ToDictionaryAsync(row => row.EntityId, row => (row.Messages, row.Xp), cancellationToken);
        var guildActivity = await activityQuery
            .GroupBy(activity => activity.GuildId)
            .Select(group => new
            {
                EntityId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .ToDictionaryAsync(row => row.EntityId, row => (row.Messages, row.Xp), cancellationToken);
        var channelActivityRows = await activityQuery
            .GroupBy(activity => activity.DiscordChannelId)
            .Select(group => new
            {
                DiscordChannelId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .ToListAsync(cancellationToken);
        List<ulong> channelDiscordIds = [.. channelActivityRows.Select(row => row.DiscordChannelId).Distinct()];
        Dictionary<ulong, int> channelEntityIds = channelDiscordIds.Count == 0
            ? []
            : await dbContext.Channels
                .AsNoTracking()
                .Where(channel => channelDiscordIds.Contains(channel.DiscordId))
                .ToDictionaryAsync(channel => channel.DiscordId, channel => channel.Id, cancellationToken);
        Dictionary<int, (long Messages, long Xp)> channelActivity = [];
        foreach (var row in channelActivityRows)
        {
            if (channelEntityIds.TryGetValue(row.DiscordChannelId, out int channelId))
                channelActivity[channelId] = (row.Messages, row.Xp);
        }

        return
        [
            .. stocks
                .Select(stock =>
                {
                    (long messages, long xp) = stock.EntityType switch
                    {
                        StockEntityType.User => userActivity.GetValueOrDefault(stock.EntityId),
                        StockEntityType.Guild => guildActivity.GetValueOrDefault(stock.EntityId),
                        StockEntityType.Channel => channelActivity.GetValueOrDefault(stock.EntityId),
                        _ => (0L, 0L)
                    };
                    return new DashboardStockActivityPricePoint(
                        stock.StockId,
                        names.GetValueOrDefault(stock.StockId, $"Stock #{stock.StockId}"),
                        EntityTypeLabel(stock.EntityType),
                        messages,
                        xp,
                        stock.Price,
                        stock.DailyChangePercent,
                        holdingValues.GetValueOrDefault(stock.StockId));
                })
                .OrderByDescending(point => point.Xp)
                .ThenByDescending(point => Math.Abs(point.DailyChangePercent))
                .Take(30)
        ];
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
        IQueryable<ButtonGamePress> allTimeQuery = dbContext.ButtonGamePresses.AsNoTracking();

        if (guildId.HasValue)
        {
            query = query.Where(press => press.GuildId == guildId.Value);
            allTimeQuery = allTimeQuery.Where(press => press.GuildId == guildId.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(press => press.UserId == userId.Value);
            allTimeQuery = allTimeQuery.Where(press => press.UserId == userId.Value);
        }

        List<ButtonGameInsightRow> rows = await query
            .Select(press => new ButtonGameInsightRow(
                press.Id,
                press.UserId,
                press.GuildId,
                press.Score,
                press.InsertDate))
            .ToListAsync(cancellationToken);

        List<DashboardButtonGamePoint> daily = BuildButtonGameDaily(rows, startDate, days);
        List<int> userIds = [.. rows.Select(row => row.UserId).Distinct()];
        Dictionary<int, (string DiscordId, string Username)> userLabels = await GetUserLabelsAsync(userIds, cancellationToken);
        Dictionary<int, string> guildLabels = await GetGuildLabelsAsync(
            rows.Select(row => row.GuildId).Where(id => id.HasValue).Select(id => id!.Value),
            cancellationToken);

        List<DashboardButtonGameUser> usersByScore = BuildButtonGameUsers(
            rows,
            userLabels,
            user => user.Score,
            descending: true,
            limit: 12);
        List<DashboardButtonGameUser> usersByPressCount = BuildButtonGameUsers(
            rows,
            userLabels,
            user => user.Presses,
            descending: true,
            limit: 12);

        long score = rows.Sum(row => row.Score);
        IReadOnlyList<DashboardButtonGameScoreEntry> topGlobalScores = BuildButtonScoreEntries(
            rows.OrderByDescending(row => row.Score).ThenByDescending(row => row.InsertDate).Take(12),
            userLabels,
            guildLabels);
        IReadOnlyList<DashboardButtonGameScoreEntry> topServerScores = BuildButtonScoreEntries(
            rows
                .Where(row => row.GuildId.HasValue)
                .GroupBy(row => row.GuildId!.Value)
                .Select(group => group.OrderByDescending(row => row.Score).ThenByDescending(row => row.InsertDate).First())
                .OrderByDescending(row => row.Score)
                .ThenByDescending(row => row.InsertDate)
                .Take(12),
            userLabels,
            guildLabels);
        IReadOnlyList<DashboardButtonGameScoreEntry> topIndividualScores = BuildButtonScoreEntries(
            rows
                .GroupBy(row => row.UserId)
                .Select(group => group.OrderByDescending(row => row.Score).ThenByDescending(row => row.InsertDate).First())
                .OrderByDescending(row => row.Score)
                .ThenByDescending(row => row.InsertDate)
                .Take(12),
            userLabels,
            guildLabels);
        IReadOnlyList<DashboardCategoryValue> pressesByServer = BuildButtonPressesByServer(rows, guildLabels);
        IReadOnlyList<DashboardButtonGameServer> competitiveServers = BuildCompetitiveButtonServers(rows, guildLabels);
        IReadOnlyList<DashboardCalendarActivityCell> calendarHeatmap = BuildButtonCalendarHeatmap(rows, startDate, days);
        IReadOnlyList<DashboardHeatmapCell> hourByWeekdayHeatmap = BuildButtonHourByWeekdayHeatmap(rows);
        IReadOnlyList<DashboardButtonGameGap> longestGaps = BuildButtonGameGaps(rows);
        long highestScoreEver = await allTimeQuery
            .Select(press => (long?)press.Score)
            .MaxAsync(cancellationToken) ?? 0;
        double medianScore = rows.Count == 0
            ? 0.0
            : Math.Round((double)Median([.. rows.Select(row => (decimal)row.Score).OrderBy(score => score)]), 1);

        return new DashboardButtonGameInsights(
            Presses: rows.Count,
            Score: score,
            AverageScore: rows.Count == 0 ? 0.0 : Math.Round((double)score / rows.Count, 1),
            MedianScore: medianScore,
            HighestScoreEver: highestScoreEver,
            LastPressAtUtc: rows.Count == 0 ? null : rows.Max(row => row.InsertDate),
            Daily: daily,
            Leaders: usersByScore.Take(8).ToList(),
            TopGlobalScores: topGlobalScores,
            TopServerScores: topServerScores,
            TopIndividualScores: topIndividualScores,
            TopUsersByTotalScore: usersByScore,
            TopUsersByPressCount: usersByPressCount,
            ScoreDistribution: BuildButtonScoreDistribution(rows.Select(row => row.Score)),
            PressesByServer: pressesByServer,
            PressesByHour:
            [
                .. rows
                    .GroupBy(row => row.InsertDate.Hour)
                    .OrderBy(group => group.Key)
                    .Select(group => new DashboardCategoryValue($"{group.Key:00}:00", group.Count()))
            ],
            PressesByWeekday:
            [
                .. rows
                    .GroupBy(row => (int)row.InsertDate.DayOfWeek)
                    .OrderBy(group => group.Key)
                    .Select(group => new DashboardCategoryValue(DayLabels[group.Key], group.Count()))
            ],
            HourByWeekdayHeatmap: hourByWeekdayHeatmap,
            CalendarHeatmap: calendarHeatmap,
            LongestGaps: longestGaps,
            CompetitiveServers: competitiveServers);
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
        var guildScope = guildId.HasValue
            ? await dbContext.Guilds
                .AsNoTracking()
                .Where(guild => guild.Id == guildId.Value)
                .Select(guild => new { guild.DiscordId, guild.Name })
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        ulong? userDiscordId = userId.HasValue
            ? await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id == userId.Value)
                .Select(user => (ulong?)user.DiscordId)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        ulong? guildDiscordId = guildScope?.DiscordId;

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

        logs = FilterScopedLogs(logs, guildDiscordId, guildScope?.Name, userDiscordId, channelDiscordId);

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
        IReadOnlyList<DashboardCategoryValue> logsByVersion =
        [
            .. logs
                .GroupBy(log => string.IsNullOrWhiteSpace(log.Version) ? "Unknown" : log.Version)
                .Select(group => new DashboardCategoryValue(group.Key, group.Count()))
                .OrderByDescending(point => point.Value)
                .ThenBy(point => point.Label)
                .Take(12)
        ];
        IReadOnlyList<DashboardCategoryValue> commonMessages =
        [
            .. logs
                .GroupBy(log => NormalizeLogMessage(log.Message))
                .Select(group => new DashboardCategoryValue(group.Key, group.Count()))
                .OrderByDescending(point => point.Value)
                .ThenBy(point => point.Label)
                .Take(12)
        ];
        IReadOnlyList<DashboardLogItem> recentIncidents =
        [
            .. logs
                .Where(log => log.Severity <= 2)
                .OrderByDescending(log => log.InsertedAtUtc)
                .Take(12)
                .Select(log => new DashboardLogItem(
                    log.Id,
                    LogSeverityLabel(log.Severity),
                    log.Message,
                    log.Version,
                    log.InsertedAtUtc))
        ];
        int warnings = logs.Count(log => log.Severity == 2);
        int errors = logs.Count(log => log.Severity == 1);
        int critical = logs.Count(log => log.Severity == 0);
        DashboardLogInsights logInsights = new(
            Total: logs.Count,
            Warnings: warnings,
            Errors: errors,
            Critical: critical,
            LatestAtUtc: logs.Count == 0 ? null : logs.Max(log => log.InsertedAtUtc),
            SeverityCounts: logSeverities,
            Timeline: logTimeline,
            Recent: recentLogs,
            LogsByVersion: logsByVersion,
            CommonMessages: commonMessages,
            RecentIncidents: recentIncidents,
            HealthIndicators: BuildLogHealthIndicators(logs.Count, warnings, errors, critical));

        return new DashboardOperationsInsights(
            reminders,
            moderation,
            logInsights,
            logSeverities,
            logTimeline,
            recentLogs);
    }

    private async Task<DashboardServerInsights?> BuildServerInsightsAsync(
        int guildId,
        IReadOnlyList<ScopedUserRow> scopedUsers,
        IQueryable<UserActivity> activityQuery,
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        int minActivity,
        CancellationToken cancellationToken)
    {
        Guild? guild = await dbContext.Guilds
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == guildId, cancellationToken);

        if (guild is null)
            return null;

        DashboardServerConfiguration configuration = await BuildServerConfigurationAsync(guild, cancellationToken);
        IReadOnlyList<DashboardServerChecklistItem> checklist = await BuildServerConfigurationChecklistAsync(
            guild,
            configuration,
            cancellationToken);
        DashboardServerTotals totals = await BuildServerTotalsAsync(
            guild,
            scopedUsers,
            activityQuery,
            startDate,
            endExclusiveDate,
            cancellationToken);
        IReadOnlyList<DailyActivityAggregate> dailyActivity = await BuildDailyActivityAggregatesAsync(
            activityQuery,
            startDate,
            days,
            cancellationToken);
        IReadOnlyList<DashboardRankedUserMetric> topAverageLengthUsers = await BuildTopAverageLengthUsersAsync(
            activityQuery,
            minActivity,
            cancellationToken);
        var trends = await BuildUserTrendInsightsAsync(
            activityQuery,
            startDate,
            endExclusiveDate,
            cancellationToken);
        IReadOnlyList<DashboardChannelActivity> quietestChannels = await BuildChannelActivityAsync(
            activityQuery,
            "asc",
            minActivity,
            cancellationToken);
        IReadOnlyList<DashboardTimeBucket> bestActivityDays = BuildBestActivityDays(dailyActivity);
        IReadOnlyList<DashboardTimeBucket> worstActivityDays = BuildWorstActivityDays(dailyActivity);
        IReadOnlyList<DashboardTimeBucket> peakHours = await BuildPeakHoursAsync(activityQuery, cancellationToken);
        IReadOnlyList<DashboardTimeBucket> peakWeekdays = await BuildPeakWeekdaysAsync(activityQuery, cancellationToken);
        IReadOnlyList<DashboardCategoryValue> activityRoleDistribution = BuildActivityRoleDistribution(totals.KnownUsers);
        IReadOnlyList<DashboardChannelHeatmapCell> channelHeatmap = await BuildChannelHeatmapAsync(
            activityQuery,
            startDate,
            days,
            cancellationToken);
        List<LogInsightRow> scopedLogs = await BuildScopedLogRowsAsync(
            guild.DiscordId,
            guild.Name,
            null,
            null,
            startDate,
            endExclusiveDate,
            cancellationToken);
        int activeUsersInWindow = await activityQuery
            .Select(activity => activity.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
        int activeChannelsInWindow = await activityQuery
            .Select(activity => activity.DiscordChannelId)
            .Distinct()
            .CountAsync(cancellationToken);
        DashboardServerHealthScorecard health = BuildServerHealthScorecard(
            totals,
            dailyActivity,
            checklist,
            trends.TrendPercent,
            activeUsersInWindow,
            activeChannelsInWindow,
            scopedLogs.Count(log => log.Severity <= 1));

        return new DashboardServerInsights(
            new DashboardServerIdentity(
                guild.Id,
                guild.DiscordId.ToString(),
                guild.Name,
                guild.InsertDate),
            totals,
            configuration,
            health,
            checklist,
            topAverageLengthUsers,
            trends.FastestRising,
            trends.Dropping,
            quietestChannels,
            bestActivityDays,
            worstActivityDays,
            peakHours,
            peakWeekdays,
            activityRoleDistribution,
            trends.RankMovement,
            channelHeatmap);
    }

    private async Task<DashboardServerConfiguration> BuildServerConfigurationAsync(
        Guild guild,
        CancellationToken cancellationToken)
    {
        ulong[] channelIds =
        [
            guild.WelcomeChannelId,
            guild.PinsChannelId,
            guild.HoneypotChannelId,
            guild.LevelUpMessagesChannelId,
            guild.LevelUpQuotesChannelId,
            guild.QuotesApprovalChannelId
        ];
        Dictionary<ulong, string> channelLabels = await GetChannelLabelsAsync(
            channelIds.Where(channelId => channelId > 0UL),
            cancellationToken);

        return new DashboardServerConfiguration(
            string.IsNullOrWhiteSpace(guild.Prefix) ? "m!" : guild.Prefix,
            BuildConfiguredChannel(guild.WelcomeChannelId),
            BuildConfiguredChannel(guild.PinsChannelId),
            BuildConfiguredChannel(guild.HoneypotChannelId),
            guild.SendHoneypotMessages,
            guild.LevelUpMessages,
            BuildConfiguredChannel(guild.LevelUpMessagesChannelId),
            guild.LevelUpQuotes,
            BuildConfiguredChannel(guild.LevelUpQuotesChannelId),
            BuildConfiguredChannel(guild.QuotesApprovalChannelId),
            guild.QuoteAddRequiredApprovals,
            guild.QuoteRemoveRequiredApprovals,
            guild.UseGlobalQuotes,
            guild.UseActivityRoles);

        DashboardConfiguredChannel BuildConfiguredChannel(ulong channelId)
        {
            if (channelId == 0UL)
                return new DashboardConfiguredChannel(string.Empty, "Not configured", false);

            return new DashboardConfiguredChannel(
                channelId.ToString(),
                channelLabels.GetValueOrDefault(channelId, $"channel-{ShortDiscordId(channelId)}"),
                true);
        }
    }

    private async Task<IReadOnlyList<DashboardServerChecklistItem>> BuildServerConfigurationChecklistAsync(
        Guild guild,
        DashboardServerConfiguration configuration,
        CancellationToken cancellationToken)
    {
        int activityRoleCount = await dbContext.Roles
            .AsNoTracking()
            .CountAsync(role => role.GuildId == guild.Id, cancellationToken);

        return
        [
            ChecklistItem(
                "Prefix",
                !string.IsNullOrWhiteSpace(configuration.Prefix),
                string.IsNullOrWhiteSpace(configuration.Prefix) ? "Missing command prefix" : configuration.Prefix),
            ChecklistItem(
                "Welcome channel",
                !guild.WelcomeMessages || configuration.WelcomeChannel.Configured,
                guild.WelcomeMessages
                    ? $"Using {configuration.WelcomeChannel.Name}"
                    : "Welcome messages disabled",
                required: false),
            ChecklistItem(
                "Pins channel",
                configuration.PinsChannel.Configured,
                configuration.PinsChannel.Configured ? configuration.PinsChannel.Name : "No pins channel selected",
                required: false),
            ChecklistItem(
                "Honeypot channel",
                !configuration.HoneypotMessages || configuration.HoneypotChannel.Configured,
                configuration.HoneypotMessages
                    ? configuration.HoneypotChannel.Name
                    : "Honeypot messages disabled",
                required: configuration.HoneypotMessages),
            ChecklistItem(
                "Level-up channel",
                !configuration.LevelUpMessages || configuration.LevelUpMessageChannel.Configured,
                configuration.LevelUpMessages
                    ? configuration.LevelUpMessageChannel.Name
                    : "Level-up messages disabled",
                required: configuration.LevelUpMessages),
            ChecklistItem(
                "Level-up quote channel",
                !configuration.LevelUpQuoteMessages || configuration.LevelUpQuoteChannel.Configured,
                configuration.LevelUpQuoteMessages
                    ? configuration.LevelUpQuoteChannel.Name
                    : "Level-up quote messages disabled",
                required: configuration.LevelUpQuoteMessages),
            ChecklistItem(
                "Quote approval channel",
                configuration.QuoteApprovalChannel.Configured,
                configuration.QuoteApprovalChannel.Configured
                    ? configuration.QuoteApprovalChannel.Name
                    : "Quote approvals have no channel",
                required: true),
            ChecklistItem(
                "Quote thresholds",
                configuration.QuoteAddRequiredApprovals > 0 && configuration.QuoteRemoveRequiredApprovals > 0,
                $"+{configuration.QuoteAddRequiredApprovals} / -{configuration.QuoteRemoveRequiredApprovals}",
                required: true),
            ChecklistItem(
                "Activity roles",
                !configuration.ActivityRoles || activityRoleCount > 0,
                configuration.ActivityRoles
                    ? $"{activityRoleCount} configured role records"
                    : "Activity roles disabled",
                required: configuration.ActivityRoles)
        ];
    }

    private static DashboardServerChecklistItem ChecklistItem(
        string label,
        bool passed,
        string detail,
        bool required = true)
    {
        string severity = passed
            ? "success"
            : required ? "danger" : "warning";

        return new DashboardServerChecklistItem(label, passed, detail, severity);
    }

    private async Task<DashboardServerTotals> BuildServerTotalsAsync(
        Guild guild,
        IReadOnlyList<ScopedUserRow> scopedUsers,
        IQueryable<UserActivity> activityQuery,
        DateTime startDate,
        DateTime endExclusiveDate,
        CancellationToken cancellationToken)
    {
        int guildId = guild.Id;
        List<int> scopedUserIds = [.. scopedUsers.Select(user => user.UserId)];
        long trackedMessages = await dbContext.UserActivity
            .AsNoTracking()
            .LongCountAsync(activity => activity.GuildId == guildId, cancellationToken);
        long totalXp = await dbContext.UserLevels
            .AsNoTracking()
            .Where(levels => levels.GuildId == guildId)
            .SumAsync(levels => (long?)levels.TotalXp, cancellationToken) ?? 0L;
        int totalQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote => quote.GuildId == guildId, cancellationToken);
        int approvedQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote => quote.GuildId == guildId && quote.Approved && !quote.Removed, cancellationToken);
        int pendingQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote => quote.GuildId == guildId && !quote.Approved && !quote.Removed, cancellationToken);
        int removedQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote => quote.GuildId == guildId && quote.Removed, cancellationToken);
        int pendingQuoteApprovals = await dbContext.QuoteApprovalMessages
            .AsNoTracking()
            .Where(approval => !approval.Approved)
            .Join(
                dbContext.Quotes.AsNoTracking().Where(quote => quote.GuildId == guildId),
                approval => approval.QuoteId,
                quote => quote.Id,
                (approval, quote) => approval.Id)
            .CountAsync(cancellationToken);
        int activeReminders = await dbContext.Reminders
            .AsNoTracking()
            .CountAsync(reminder => reminder.GuildId == guildId, cancellationToken);
        long buttonPresses = await dbContext.ButtonGamePresses
            .AsNoTracking()
            .LongCountAsync(press => press.GuildId == guildId, cancellationToken);
        DateTime? lastActivityAtUtc = await dbContext.UserActivity
            .AsNoTracking()
            .Where(activity => activity.GuildId == guildId)
            .Select(activity => (DateTime?)activity.InsertDate)
            .MaxAsync(cancellationToken);

        IQueryable<StockTransaction> transactionQuery = dbContext.StockTransactions
            .AsNoTracking()
            .Where(transaction => transaction.InsertDate >= startDate && transaction.InsertDate < endExclusiveDate);
        transactionQuery = scopedUserIds.Count == 0
            ? transactionQuery.Where(_ => false)
            : transactionQuery.Where(transaction =>
                scopedUserIds.Contains(transaction.UserId) ||
                (transaction.TargetUserId.HasValue && scopedUserIds.Contains(transaction.TargetUserId.Value)));

        long economyTransactions = await transactionQuery.LongCountAsync(cancellationToken);
        List<decimal> transactionAmounts = await transactionQuery
            .Select(transaction => transaction.Amount)
            .ToListAsync(cancellationToken);
        decimal economyVolume = transactionAmounts.Sum(Math.Abs);
        int stockWindowDays = Math.Max(1, (int)(endExclusiveDate.Date - startDate.Date).TotalDays);
        DashboardStockMarketInsights stockMarket = await BuildStockMarketInsightsAsync(
            guildId,
            null,
            null,
            scopedUserIds,
            activityQuery,
            startDate,
            endExclusiveDate,
            stockWindowDays,
            cancellationToken);

        return new DashboardServerTotals(
            scopedUsers.Count,
            trackedMessages,
            totalXp,
            totalQuotes,
            approvedQuotes,
            pendingQuotes,
            pendingQuoteApprovals,
            removedQuotes,
            activeReminders,
            buttonPresses,
            economyTransactions,
            economyVolume,
            stockMarket.Stocks,
            stockMarket.MarketValue,
            lastActivityAtUtc);
    }

    private async Task<IReadOnlyList<DailyActivityAggregate>> BuildDailyActivityAggregatesAsync(
        IQueryable<UserActivity> query,
        DateTime startDate,
        int days,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.InsertDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                ActiveUsers = group.Select(activity => activity.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);
        Dictionary<DateTime, DailyActivityAggregate> byDate = rows.ToDictionary(
            row => row.Date.Date,
            row => new DailyActivityAggregate(row.Date.Date, row.Messages, row.Xp, row.ActiveUsers));

        List<DailyActivityAggregate> daily = [];
        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset).Date;
            daily.Add(byDate.GetValueOrDefault(date, new DailyActivityAggregate(date, 0, 0L, 0)));
        }

        return daily;
    }

    private async Task<IReadOnlyList<DashboardRankedUserMetric>> BuildTopAverageLengthUsersAsync(
        IQueryable<UserActivity> query,
        int minActivity,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Messages = group.LongCount(),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0,
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            })
            .Where(user => user.Messages >= minActivity)
            .OrderByDescending(user => user.AverageMessageLength)
            .ThenByDescending(user => user.Messages)
            .Take(10)
            .ToListAsync(cancellationToken);
        Dictionary<int, (string DiscordId, string Username)> userLabels = await GetUserLabelsAsync(
            rows.Select(row => row.UserId),
            cancellationToken);

        return
        [
            .. rows.Select((row, index) =>
            {
                (string discordId, string username) = userLabels.GetValueOrDefault(row.UserId, (string.Empty, "Unknown"));
                return new DashboardRankedUserMetric(
                    index + 1,
                    row.UserId,
                    discordId,
                    username,
                    Math.Round(row.AverageMessageLength, 1),
                    "chars",
                    row.LastActivityAtUtc);
            })
        ];
    }

    private async Task<(
        IReadOnlyList<DashboardUserTrend> FastestRising,
        IReadOnlyList<DashboardUserTrend> Dropping,
        IReadOnlyList<DashboardUserRankMovement> RankMovement,
        double TrendPercent)> BuildUserTrendInsightsAsync(
        IQueryable<UserActivity> query,
        DateTime startDate,
        DateTime endExclusiveDate,
        CancellationToken cancellationToken)
    {
        DateTime split = startDate + TimeSpan.FromTicks(Math.Max(1L, (endExclusiveDate - startDate).Ticks / 2));
        var rows = await query
            .Select(activity => new
            {
                activity.UserId,
                activity.XpGained,
                activity.InsertDate
            })
            .ToListAsync(cancellationToken);

        Dictionary<int, long> previousMessages = rows
            .Where(row => row.InsertDate < split)
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => (long)group.Count());
        Dictionary<int, long> recentMessages = rows
            .Where(row => row.InsertDate >= split)
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => (long)group.Count());
        Dictionary<int, long> previousXp = rows
            .Where(row => row.InsertDate < split)
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Sum(row => (long)row.XpGained));
        Dictionary<int, long> recentXp = rows
            .Where(row => row.InsertDate >= split)
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Sum(row => (long)row.XpGained));

        List<int> userIds =
        [
            .. previousMessages.Keys
                .Concat(recentMessages.Keys)
                .Concat(previousXp.Keys)
                .Concat(recentXp.Keys)
                .Distinct()
        ];
        Dictionary<int, (string DiscordId, string Username)> userLabels = await GetUserLabelsAsync(userIds, cancellationToken);

        List<DashboardUserTrend> trends =
        [
            .. userIds.Select(userId =>
            {
                long previous = previousMessages.GetValueOrDefault(userId);
                long recent = recentMessages.GetValueOrDefault(userId);
                long delta = recent - previous;
                double deltaPercent = previous == 0
                    ? recent == 0 ? 0.0 : 100.0
                    : Math.Round((double)delta / previous * 100.0, 1);
                (string discordId, string username) = userLabels.GetValueOrDefault(userId, (string.Empty, "Unknown"));
                return new DashboardUserTrend(
                    0,
                    userId,
                    discordId,
                    username,
                    previous,
                    recent,
                    delta,
                    deltaPercent);
            })
        ];

        IReadOnlyList<DashboardUserTrend> fastestRising =
        [
            .. trends
                .Where(trend => trend.Delta > 0)
                .OrderByDescending(trend => trend.Delta)
                .ThenByDescending(trend => trend.RecentMessages)
                .Take(8)
                .Select((trend, index) => trend with { Rank = index + 1 })
        ];
        IReadOnlyList<DashboardUserTrend> dropping =
        [
            .. trends
                .Where(trend => trend.Delta < 0)
                .OrderBy(trend => trend.Delta)
                .ThenByDescending(trend => trend.PreviousMessages)
                .Take(8)
                .Select((trend, index) => trend with { Rank = index + 1 })
        ];

        Dictionary<int, int> previousRanks = previousXp
            .OrderByDescending(row => row.Value)
            .ThenBy(row => row.Key)
            .Select((row, index) => new { row.Key, Rank = index + 1 })
            .ToDictionary(row => row.Key, row => row.Rank);
        List<KeyValuePair<int, long>> currentRankRows =
        [
            .. recentXp
                .OrderByDescending(row => row.Value)
                .ThenBy(row => row.Key)
                .Take(10)
        ];
        IReadOnlyList<DashboardUserRankMovement> rankMovement =
        [
            .. currentRankRows.Select((row, index) =>
            {
                int currentRank = index + 1;
                int? previousRank = previousRanks.TryGetValue(row.Key, out int rank) ? rank : null;
                (string discordId, string username) = userLabels.GetValueOrDefault(row.Key, (string.Empty, "Unknown"));
                return new DashboardUserRankMovement(
                    row.Key,
                    discordId,
                    username,
                    previousRank,
                    currentRank,
                    previousRank.HasValue ? previousRank.Value - currentRank : 0,
                    previousXp.GetValueOrDefault(row.Key),
                    row.Value);
            })
        ];

        double previousAverage = previousMessages.Count == 0 ? 0.0 : previousMessages.Values.Average();
        double recentAverage = recentMessages.Count == 0 ? 0.0 : recentMessages.Values.Average();
        double trendPercent = previousAverage == 0.0
            ? recentAverage == 0.0 ? 0.0 : 100.0
            : Math.Round((recentAverage - previousAverage) / previousAverage * 100.0, 1);

        return (fastestRising, dropping, rankMovement, trendPercent);
    }

    private static IReadOnlyList<DashboardTimeBucket> BuildBestActivityDays(
        IReadOnlyList<DailyActivityAggregate> dailyActivity) =>
        [
            .. dailyActivity
                .OrderByDescending(day => day.Messages)
                .ThenBy(day => day.Date)
                .Take(7)
                .Select(day => new DashboardTimeBucket(
                    day.Date.ToString("yyyy-MM-dd"),
                    (int)(day.Date - DateTime.UnixEpoch).TotalDays,
                    day.Messages,
                    day.Xp,
                    day.ActiveUsers))
        ];

    private static IReadOnlyList<DashboardTimeBucket> BuildWorstActivityDays(
        IReadOnlyList<DailyActivityAggregate> dailyActivity) =>
        [
            .. dailyActivity
                .OrderBy(day => day.Messages)
                .ThenBy(day => day.Date)
                .Take(7)
                .Select(day => new DashboardTimeBucket(
                    day.Date.ToString("yyyy-MM-dd"),
                    (int)(day.Date - DateTime.UnixEpoch).TotalDays,
                    day.Messages,
                    day.Xp,
                    day.ActiveUsers))
        ];

    private async Task<IReadOnlyList<DashboardTimeBucket>> BuildPeakHoursAsync(
        IQueryable<UserActivity> query,
        CancellationToken cancellationToken)
    {
        var messageRows = await query
            .GroupBy(activity => activity.InsertDate.Hour)
            .Select(group => new
            {
                Hour = group.Key,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .ToListAsync(cancellationToken);
        var userRows = await query
            .GroupBy(activity => new { activity.InsertDate.Hour, activity.UserId })
            .Select(group => new { group.Key.Hour, group.Key.UserId })
            .ToListAsync(cancellationToken);
        Dictionary<int, int> activeUsers = userRows
            .GroupBy(row => row.Hour)
            .ToDictionary(group => group.Key, group => group.Count());

        return
        [
            .. messageRows
                .OrderByDescending(row => row.Messages)
                .ThenBy(row => row.Hour)
                .Take(6)
                .Select(row => new DashboardTimeBucket(
                    $"{row.Hour:00}:00 UTC",
                    row.Hour,
                    row.Messages,
                    row.Xp,
                    activeUsers.GetValueOrDefault(row.Hour)))
        ];
    }

    private async Task<IReadOnlyList<DashboardTimeBucket>> BuildPeakWeekdaysAsync(
        IQueryable<UserActivity> query,
        CancellationToken cancellationToken)
    {
        var messageRows = await query
            .GroupBy(activity => activity.InsertDate.DayOfWeek)
            .Select(group => new
            {
                Day = group.Key,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .ToListAsync(cancellationToken);
        var userRows = await query
            .GroupBy(activity => new { activity.InsertDate.DayOfWeek, activity.UserId })
            .Select(group => new { group.Key.DayOfWeek, group.Key.UserId })
            .ToListAsync(cancellationToken);
        Dictionary<DayOfWeek, int> activeUsers = userRows
            .GroupBy(row => row.DayOfWeek)
            .ToDictionary(group => group.Key, group => group.Count());

        return
        [
            .. messageRows
                .OrderByDescending(row => row.Messages)
                .ThenBy(row => row.Day)
                .Select(row => new DashboardTimeBucket(
                    DayLabels[(int)row.Day],
                    (int)row.Day,
                    row.Messages,
                    row.Xp,
                    activeUsers.GetValueOrDefault(row.Day)))
        ];
    }

    private static IReadOnlyList<DashboardCategoryValue> BuildActivityRoleDistribution(int knownUsers)
    {
        if (knownUsers <= 0)
            return [];

        int topOne = PercentileCount(knownUsers, 1);
        int topFive = PercentileCount(knownUsers, 5);
        int topTen = PercentileCount(knownUsers, 10);
        int topTwenty = PercentileCount(knownUsers, 20);
        int topThirty = PercentileCount(knownUsers, 30);

        return
        [
            new DashboardCategoryValue("Top 1%", topOne),
            new DashboardCategoryValue("1-5%", Math.Max(0, topFive - topOne)),
            new DashboardCategoryValue("5-10%", Math.Max(0, topTen - topFive)),
            new DashboardCategoryValue("10-20%", Math.Max(0, topTwenty - topTen)),
            new DashboardCategoryValue("20-30%", Math.Max(0, topThirty - topTwenty)),
            new DashboardCategoryValue("Other tracked users", Math.Max(0, knownUsers - topThirty))
        ];

        static int PercentileCount(int total, int percentile) =>
            Math.Min(total, Math.Max(1, (int)Math.Ceiling(total * (percentile / 100.0))));
    }

    private async Task<IReadOnlyList<DashboardChannelHeatmapCell>> BuildChannelHeatmapAsync(
        IQueryable<UserActivity> query,
        DateTime startDate,
        int days,
        CancellationToken cancellationToken)
    {
        List<ulong> topChannelIds = await query
            .GroupBy(activity => activity.DiscordChannelId)
            .Select(group => new
            {
                ChannelId = group.Key,
                Messages = group.Count()
            })
            .OrderByDescending(row => row.Messages)
            .Take(8)
            .Select(row => row.ChannelId)
            .ToListAsync(cancellationToken);

        if (topChannelIds.Count == 0)
            return [];

        var rows = await query
            .Where(activity => topChannelIds.Contains(activity.DiscordChannelId))
            .GroupBy(activity => new
            {
                Date = activity.InsertDate.Date,
                activity.DiscordChannelId
            })
            .Select(group => new
            {
                group.Key.Date,
                ChannelId = group.Key.DiscordChannelId,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .ToListAsync(cancellationToken);
        Dictionary<ulong, string> channelLabels = await GetChannelLabelsAsync(topChannelIds, cancellationToken);
        Dictionary<(DateTime Date, ulong ChannelId), (int Messages, long Xp)> byDateAndChannel = rows.ToDictionary(
            row => (row.Date.Date, row.ChannelId),
            row => (row.Messages, row.Xp));

        List<DashboardChannelHeatmapCell> cells = [];
        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset).Date;
            foreach (ulong channelId in topChannelIds)
            {
                var cell = byDateAndChannel.GetValueOrDefault((date, channelId));
                cells.Add(new DashboardChannelHeatmapCell(
                    DateTime.SpecifyKind(date, DateTimeKind.Utc),
                    channelId.ToString(),
                    channelLabels.GetValueOrDefault(channelId, $"channel-{ShortDiscordId(channelId)}"),
                    cell.Messages,
                    cell.Xp));
            }
        }

        return cells;
    }

    private async Task<List<LogInsightRow>> BuildScopedLogRowsAsync(
        ulong? guildDiscordId,
        string? guildName,
        ulong? userDiscordId,
        ulong? channelDiscordId,
        DateTime startDate,
        DateTime endExclusiveDate,
        CancellationToken cancellationToken)
    {
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

        return FilterScopedLogs(logs, guildDiscordId, guildName, userDiscordId, channelDiscordId);
    }

    private static List<LogInsightRow> FilterScopedLogs(
        List<LogInsightRow> logs,
        ulong? guildDiscordId,
        string? guildName,
        ulong? userDiscordId,
        ulong? channelDiscordId)
    {
        List<string> tokens = [];

        if (guildDiscordId.HasValue)
            tokens.Add(guildDiscordId.Value.ToString());

        if (!string.IsNullOrWhiteSpace(guildName) && guildName.Trim().Length >= 3)
            tokens.Add(guildName.Trim());

        if (userDiscordId.HasValue)
            tokens.Add(userDiscordId.Value.ToString());

        if (channelDiscordId.HasValue)
            tokens.Add(channelDiscordId.Value.ToString());

        return tokens.Count == 0
            ? logs
            : [.. logs.Where(log => tokens.Any(token => log.Message.Contains(token, StringComparison.OrdinalIgnoreCase)))];
    }

    private DashboardServerHealthScorecard BuildServerHealthScorecard(
        DashboardServerTotals totals,
        IReadOnlyList<DailyActivityAggregate> dailyActivity,
        IReadOnlyList<DashboardServerChecklistItem> checklist,
        double trendPercent,
        int activeUsers,
        int activeChannels,
        int severeLogs)
    {
        long windowMessages = dailyActivity.Sum(day => (long)day.Messages);
        int activeDays = dailyActivity.Count(day => day.Messages > 0);
        double messagesPerDay = dailyActivity.Count == 0 ? 0.0 : (double)windowMessages / dailyActivity.Count;
        int activityScore = ClampScore(
            (messagesPerDay >= 100.0 ? 45.0 : messagesPerDay / 100.0 * 45.0) +
            (activeDays >= Math.Min(7, dailyActivity.Count) ? 20.0 : activeDays / (double)Math.Max(1, Math.Min(7, dailyActivity.Count)) * 20.0) +
            (trendPercent >= 0.0 ? 20.0 : Math.Max(0.0, 20.0 + trendPercent / 5.0)) +
            (activeChannels > 0 ? 15.0 : 0.0));
        int configurationScore = checklist.Count == 0
            ? 100
            : ClampScore(checklist.Count(item => item.Passed) / (double)checklist.Count * 100.0);
        int operationsScore = ClampScore(
            100.0 -
            Math.Min(30, totals.PendingQuoteApprovals * 4) -
            Math.Min(25, totals.ActiveReminders > 25 ? (totals.ActiveReminders - 25) * 2 : 0) -
            Math.Min(30, severeLogs * 8));
        int engagementScore = totals.KnownUsers == 0
            ? 0
            : ClampScore(Math.Min(1.0, activeUsers / (double)Math.Max(1, totals.KnownUsers)) * 100.0);
        int score = ClampScore(
            activityScore * 0.35 +
            configurationScore * 0.25 +
            operationsScore * 0.20 +
            engagementScore * 0.20);
        string label = score >= 85
            ? "Strong"
            : score >= 70
                ? "Healthy"
                : score >= 50
                    ? "Needs attention"
                    : "At risk";
        List<string> notes = [];

        if (windowMessages == 0)
            notes.Add("No tracked activity in the selected window.");

        if (trendPercent < -10.0)
            notes.Add("Recent activity is lower than the first half of the selected window.");

        int failedChecklistItems = checklist.Count(item => !item.Passed);
        if (failedChecklistItems > 0)
            notes.Add($"{failedChecklistItems} configuration checks need attention.");

        if (totals.PendingQuoteApprovals > 0)
            notes.Add($"{totals.PendingQuoteApprovals} quote approvals are waiting.");

        if (severeLogs > 0)
            notes.Add($"{severeLogs} related error or critical logs were found.");

        if (notes.Count == 0)
            notes.Add("Activity, operations, and configuration look steady in this window.");

        return new DashboardServerHealthScorecard(
            score,
            label,
            activityScore,
            configurationScore,
            operationsScore,
            engagementScore,
            notes);
    }

    private static int ClampScore(double score) =>
        Math.Clamp((int)Math.Round(score), 0, 100);

    private async Task<DashboardUserProfileInsights?> BuildUserProfileInsightsAsync(
        int userId,
        int? guildId,
        IQueryable<UserActivity> activityQuery,
        DashboardActivityInsights activity,
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.DiscordId,
                candidate.Username,
                candidate.InsertDate,
                candidate.LevelUpMessages,
                candidate.LevelUpQuotes,
                candidate.Balance
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
            return null;

        IReadOnlyList<DashboardUserServerLevel> serverLevels = await BuildUserServerLevelsAsync(
            userId,
            cancellationToken);
        IReadOnlyList<DailyActivityAggregate> dailyActivity = await BuildDailyActivityAggregatesAsync(
            activityQuery,
            startDate,
            days,
            cancellationToken);
        IReadOnlyList<DashboardUserContribution> serverContribution = await BuildUserServerContributionAsync(
            activityQuery,
            cancellationToken);
        IReadOnlyList<DashboardUserContribution> channelContribution = await BuildUserChannelContributionAsync(
            activityQuery,
            cancellationToken);
        IReadOnlyList<DashboardHistogramBucket> messageLengthHistogram = await BuildMessageLengthHistogramAsync(
            activityQuery,
            cancellationToken);
        IReadOnlyList<DashboardUserMessageLengthPoint> messageLengthTrend = await BuildMessageLengthTrendAsync(
            activityQuery,
            startDate,
            days,
            cancellationToken);
        IReadOnlyList<DashboardHeatmapCell> hourByWeekday = await BuildHeatmapAsync(activityQuery, cancellationToken);
        IReadOnlyList<DashboardCalendarActivityCell> activityCalendar =
        [
            .. activity.Points.Select(point => new DashboardCalendarActivityCell(
                point.DateUtc,
                point.Messages,
                point.Xp,
                point.ActiveUsers))
        ];
        DashboardUserActivityStreaks streaks = BuildUserActivityStreaks(dailyActivity);
        DashboardUserQuotePerformance quotePerformance = await BuildUserQuotePerformanceAsync(
            userId,
            guildId,
            startDate,
            endExclusiveDate,
            cancellationToken);
        DashboardUserQuoteTotals quoteTotals = await BuildUserQuoteTotalsAsync(
            userId,
            guildId,
            cancellationToken);
        IReadOnlyList<DashboardUserStockHolding> stockHoldings = await BuildUserStockHoldingsAsync(
            userId,
            cancellationToken);
        DashboardUserEconomyPerformance economyPerformance = await BuildUserEconomyPerformanceAsync(
            userId,
            user.Balance,
            stockHoldings,
            startDate,
            endExclusiveDate,
            days,
            cancellationToken);
        DashboardUserButtonGamePerformance buttonGame = await BuildUserButtonGamePerformanceAsync(
            userId,
            guildId,
            startDate,
            endExclusiveDate,
            days,
            cancellationToken);
        IReadOnlyList<DashboardUserReminderTimelineItem> reminders = await BuildUserReminderTimelineAsync(
            userId,
            guildId,
            cancellationToken);
        (DashboardUserRankSnapshot GlobalRank, IReadOnlyList<DashboardUserRankSnapshot> ServerRanks) ranks =
            await BuildUserRankSnapshotsAsync(userId, serverLevels, cancellationToken);
        IReadOnlyList<DashboardUserRankTimelinePoint> rankMovement = await BuildUserRankTimelineAsync(
            userId,
            guildId,
            startDate,
            endExclusiveDate,
            days,
            cancellationToken);

        IReadOnlyList<DashboardUserServerLevel> scopedServerLevels = guildId.HasValue
            ? [.. serverLevels.Where(level => level.GuildId == guildId.Value)]
            : serverLevels;
        long totalXp = scopedServerLevels.Sum(level => level.TotalXp);
        long totalMessages = scopedServerLevels.Sum(level => level.Messages);
        if (totalXp == 0L)
        {
            IQueryable<UserActivity> allActivity = dbContext.UserActivity
                .AsNoTracking()
                .Where(row => row.UserId == userId);
            if (guildId.HasValue)
                allActivity = allActivity.Where(row => row.GuildId == guildId.Value);

            totalXp = await allActivity.SumAsync(row => (long?)row.XpGained, cancellationToken) ?? 0L;
            totalMessages = await allActivity.LongCountAsync(cancellationToken);
        }

        int knownChannels = await GetUserKnownChannelCountAsync(userId, guildId, cancellationToken);
        DateTime? lastActivityAtUtc = await GetUserLastActivityAsync(userId, guildId, cancellationToken);
        string mostActiveServer = serverContribution.FirstOrDefault()?.Label ??
            scopedServerLevels.OrderByDescending(level => level.Messages).FirstOrDefault()?.Name ??
            "No server activity";
        string mostActiveChannel = channelContribution.FirstOrDefault()?.Label ?? "No channel activity";
        double averageMessageLength = WeightedAverage(
            scopedServerLevels,
            level => level.Messages,
            level => level.AverageMessageLength);
        double movingAverage = WeightedAverage(
            scopedServerLevels,
            level => level.Messages,
            level => level.MessageLengthMovingAverage);
        IReadOnlyList<DashboardUserLevelPoint> levelProgression = BuildUserLevelProgression(
            activity.Points,
            totalXp);

        DashboardUserProfileTotals totals = new(
            totalXp,
            ActivityLevelService.CalculateLevel(totalXp),
            totalMessages,
            Math.Round(averageMessageLength, 1),
            Math.Round(movingAverage, 1),
            totalMessages == 0L ? 0.0 : Math.Round((double)totalXp / totalMessages, 1),
            scopedServerLevels.Count,
            knownChannels,
            mostActiveServer,
            mostActiveChannel,
            quoteTotals.Contributions,
            quoteTotals.ScoreReceived,
            quoteTotals.VotesGiven,
            user.Balance,
            economyPerformance.PortfolioValue,
            economyPerformance.NetWorth,
            buttonGame.Score,
            lastActivityAtUtc);

        return new DashboardUserProfileInsights(
            new DashboardUserProfileIdentity(
                user.Id,
                user.DiscordId.ToString(),
                user.Username,
                user.InsertDate,
                user.LevelUpMessages,
                user.LevelUpQuotes),
            totals,
            activity,
            scopedServerLevels,
            BuildBestActivityDays(dailyActivity),
            BuildWorstActivityDays(dailyActivity),
            streaks,
            serverContribution,
            channelContribution,
            ranks.GlobalRank,
            ranks.ServerRanks,
            rankMovement,
            quotePerformance,
            economyPerformance,
            stockHoldings,
            buttonGame,
            reminders,
            messageLengthHistogram,
            messageLengthTrend,
            levelProgression,
            activityCalendar,
            hourByWeekday);
    }

    private async Task<IReadOnlyList<DashboardUserServerLevel>> BuildUserServerLevelsAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var levelRows = await dbContext.UserLevels
            .AsNoTracking()
            .Where(level => level.UserId == userId)
            .Select(level => new
            {
                level.GuildId,
                level.TotalXp,
                level.UserMessageCount,
                level.UserAverageMessageLength,
                level.UserAverageMessageLengthEma
            })
            .ToListAsync(cancellationToken);
        var activityRows = await dbContext.UserActivity
            .AsNoTracking()
            .Where(activity => activity.UserId == userId)
            .GroupBy(activity => activity.GuildId)
            .Select(group => new
            {
                GuildId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0,
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            })
            .ToListAsync(cancellationToken);
        List<int> guildIds =
        [
            .. levelRows.Select(row => row.GuildId)
                .Concat(activityRows.Select(row => row.GuildId))
                .Distinct()
        ];

        if (guildIds.Count == 0)
            return [];

        Dictionary<int, (string DiscordId, string Name)> guildLabels = await dbContext.Guilds
            .AsNoTracking()
            .Where(guild => guildIds.Contains(guild.Id))
            .Select(guild => new { guild.Id, guild.DiscordId, guild.Name })
            .ToDictionaryAsync(
                guild => guild.Id,
                guild => (guild.DiscordId.ToString(), guild.Name),
                cancellationToken);
        var rankRows = await dbContext.UserLevels
            .AsNoTracking()
            .Where(level => guildIds.Contains(level.GuildId))
            .Select(level => new { level.GuildId, level.UserId, level.TotalXp })
            .ToListAsync(cancellationToken);

        return
        [
            .. guildIds.Select(guildId =>
            {
                var level = levelRows.SingleOrDefault(row => row.GuildId == guildId);
                var activity = activityRows.SingleOrDefault(row => row.GuildId == guildId);
                long totalXp = level?.TotalXp ?? activity?.Xp ?? 0L;
                long messages = level?.UserMessageCount ?? activity?.Messages ?? 0L;
                double averageLength = level?.UserAverageMessageLength ?? activity?.AverageMessageLength ?? 0.0;
                double ema = level?.UserAverageMessageLengthEma ?? averageLength;
                List<int> guildXpRows =
                [
                    .. rankRows
                        .Where(row => row.GuildId == guildId)
                        .Select(row => row.TotalXp)
                        .OrderByDescending(value => value)
                ];
                int? rowIndex = guildXpRows.FindIndex(value => value <= totalXp);
                int rank = rowIndex.HasValue && rowIndex.Value >= 0
                    ? rowIndex.Value + 1
                    : guildXpRows.Count + 1;
                (string discordId, string name) = guildLabels.GetValueOrDefault(
                    guildId,
                    (string.Empty, $"Server #{guildId}"));

                return new DashboardUserServerLevel(
                    guildId,
                    discordId,
                    name,
                    ActivityLevelService.CalculateLevel(totalXp),
                    totalXp,
                    messages,
                    Math.Round(averageLength, 1),
                    Math.Round(ema, 1),
                    rank,
                    Math.Max(guildXpRows.Count, 1),
                    activity?.LastActivityAtUtc);
            })
            .OrderByDescending(level => level.Messages)
            .ThenByDescending(level => level.TotalXp)
        ];
    }

    private async Task<IReadOnlyList<DashboardUserContribution>> BuildUserServerContributionAsync(
        IQueryable<UserActivity> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.GuildId)
            .Select(group => new
            {
                GuildId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .OrderByDescending(row => row.Messages)
            .ThenByDescending(row => row.Xp)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return [];

        List<int> rowGuildIds = [.. rows.Select(row => row.GuildId).Distinct()];
        Dictionary<int, string> guildLabels = await dbContext.Guilds
            .AsNoTracking()
            .Where(guild => rowGuildIds.Contains(guild.Id))
            .Select(guild => new { guild.Id, guild.Name })
            .ToDictionaryAsync(guild => guild.Id, guild => guild.Name, cancellationToken);
        long totalMessages = rows.Sum(row => row.Messages);

        return
        [
            .. rows.Select(row => new DashboardUserContribution(
                row.GuildId.ToString(),
                guildLabels.GetValueOrDefault(row.GuildId, $"Server #{row.GuildId}"),
                row.Messages,
                row.Xp,
                totalMessages == 0L ? 0m : Math.Round((decimal)row.Messages / totalMessages * 100m, 1)))
        ];
    }

    private async Task<IReadOnlyList<DashboardUserContribution>> BuildUserChannelContributionAsync(
        IQueryable<UserActivity> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.DiscordChannelId)
            .Select(group => new
            {
                ChannelId = group.Key,
                Messages = group.LongCount(),
                Xp = group.Sum(activity => (long)activity.XpGained)
            })
            .OrderByDescending(row => row.Messages)
            .ThenByDescending(row => row.Xp)
            .Take(12)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return [];

        Dictionary<ulong, string> channelLabels = await GetChannelLabelsAsync(
            rows.Select(row => row.ChannelId),
            cancellationToken);
        long totalMessages = rows.Sum(row => row.Messages);

        return
        [
            .. rows.Select(row => new DashboardUserContribution(
                row.ChannelId.ToString(),
                channelLabels.GetValueOrDefault(row.ChannelId, $"channel-{ShortDiscordId(row.ChannelId)}"),
                row.Messages,
                row.Xp,
                totalMessages == 0L ? 0m : Math.Round((decimal)row.Messages / totalMessages * 100m, 1)))
        ];
    }

    private async Task<IReadOnlyList<DashboardHistogramBucket>> BuildMessageLengthHistogramAsync(
        IQueryable<UserActivity> query,
        CancellationToken cancellationToken)
    {
        List<int> lengths = await query
            .Select(activity => activity.MessageLength)
            .ToListAsync(cancellationToken);

        return
        [
            new DashboardHistogramBucket("0-20", lengths.Count(length => length <= 20)),
            new DashboardHistogramBucket("21-60", lengths.Count(length => length is > 20 and <= 60)),
            new DashboardHistogramBucket("61-120", lengths.Count(length => length is > 60 and <= 120)),
            new DashboardHistogramBucket("121-240", lengths.Count(length => length is > 120 and <= 240)),
            new DashboardHistogramBucket("241+", lengths.Count(length => length > 240))
        ];
    }

    private async Task<IReadOnlyList<DashboardUserMessageLengthPoint>> BuildMessageLengthTrendAsync(
        IQueryable<UserActivity> query,
        DateTime startDate,
        int days,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .GroupBy(activity => activity.InsertDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                Messages = group.Count(),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0
            })
            .ToListAsync(cancellationToken);
        Dictionary<DateTime, (int Messages, double Average)> byDate = rows.ToDictionary(
            row => row.Date.Date,
            row => (row.Messages, row.AverageMessageLength));
        List<double> averages = [];
        List<DashboardUserMessageLengthPoint> points = [];

        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset).Date;
            (int messages, double average) = byDate.GetValueOrDefault(date);
            averages.Add(average);
            int rollingStart = Math.Max(0, averages.Count - 7);
            double movingAverage = averages
                .Skip(rollingStart)
                .Where(value => value > 0.0)
                .DefaultIfEmpty(0.0)
                .Average();

            points.Add(new DashboardUserMessageLengthPoint(
                DateTime.SpecifyKind(date, DateTimeKind.Utc),
                Math.Round(average, 1),
                Math.Round(movingAverage, 1),
                messages));
        }

        return points;
    }

    private static DashboardUserActivityStreaks BuildUserActivityStreaks(
        IReadOnlyList<DailyActivityAggregate> dailyActivity)
    {
        int current = 0;
        DateTime? currentEnd = null;
        for (int index = dailyActivity.Count - 1; index >= 0; index--)
        {
            if (dailyActivity[index].Messages <= 0)
                break;

            current++;
            currentEnd ??= dailyActivity[index].Date;
        }

        DateTime? currentStart = current > 0 && currentEnd.HasValue
            ? currentEnd.Value.AddDays(-(current - 1))
            : null;
        int longest = 0;
        int running = 0;
        DateTime? runningStart = null;
        DateTime? longestStart = null;
        DateTime? longestEnd = null;

        foreach (DailyActivityAggregate day in dailyActivity)
        {
            if (day.Messages > 0)
            {
                runningStart ??= day.Date;
                running++;
                if (running > longest)
                {
                    longest = running;
                    longestStart = runningStart;
                    longestEnd = day.Date;
                }
            }
            else
            {
                running = 0;
                runningStart = null;
            }
        }

        int activeDays = dailyActivity.Count(day => day.Messages > 0);
        return new DashboardUserActivityStreaks(
            current,
            currentStart.HasValue ? DateTime.SpecifyKind(currentStart.Value.Date, DateTimeKind.Utc) : null,
            currentEnd.HasValue ? DateTime.SpecifyKind(currentEnd.Value.Date, DateTimeKind.Utc) : null,
            longest,
            longestStart.HasValue ? DateTime.SpecifyKind(longestStart.Value.Date, DateTimeKind.Utc) : null,
            longestEnd.HasValue ? DateTime.SpecifyKind(longestEnd.Value.Date, DateTimeKind.Utc) : null,
            activeDays,
            dailyActivity.Count - activeDays);
    }

    private async Task<DashboardUserQuotePerformance> BuildUserQuotePerformanceAsync(
        int userId,
        int? guildId,
        DateTime startDate,
        DateTime endExclusiveDate,
        CancellationToken cancellationToken)
    {
        IQueryable<Quote> query = dbContext.Quotes
            .AsNoTracking()
            .Where(quote =>
                quote.UserId == userId &&
                quote.InsertDate >= startDate &&
                quote.InsertDate < endExclusiveDate);

        if (guildId.HasValue)
            query = query.Where(quote => quote.GuildId == guildId.Value);

        List<QuoteInsightRow> quotes = await query
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
        List<int> quoteIds = [.. quotes.Select(quote => quote.Id)];
        Dictionary<int, int> scores = quoteIds.Count == 0
            ? []
            : await dbContext.QuoteScores
                .AsNoTracking()
                .Where(score => quoteIds.Contains(score.QuoteId))
                .GroupBy(score => score.QuoteId)
                .Select(group => new { QuoteId = group.Key, Score = group.Sum(score => score.Score) })
                .ToDictionaryAsync(row => row.QuoteId, row => row.Score, cancellationToken);
        int votesGiven = await CountUserQuoteVotesGivenAsync(
            userId,
            guildId,
            startDate,
            endExclusiveDate,
            cancellationToken);
        List<int> quoteGuildIds = [.. quotes.Select(quote => quote.GuildId).Distinct()];
        Dictionary<int, string> guildLabels = quoteGuildIds.Count == 0
            ? []
            : await dbContext.Guilds
                .AsNoTracking()
                .Where(guild => quoteGuildIds.Contains(guild.Id))
                .Select(guild => new { guild.Id, guild.Name })
                .ToDictionaryAsync(guild => guild.Id, guild => guild.Name, cancellationToken);

        IReadOnlyList<DashboardCategoryValue> scoreByServer =
        [
            .. quotes
                .GroupBy(quote => quote.GuildId)
                .Select(group => new DashboardCategoryValue(
                    guildLabels.GetValueOrDefault(group.Key, $"Server #{group.Key}"),
                    group.Sum(quote => scores.GetValueOrDefault(quote.Id))))
                .OrderByDescending(row => row.Value)
        ];
        IReadOnlyList<DashboardQuoteItem> recentQuotes =
        [
            .. quotes
                .OrderByDescending(quote => quote.InsertedAtUtc)
                .Take(8)
                .Select(quote => new DashboardQuoteItem(
                    quote.Id,
                    quote.GuildId,
                    quote.UserId,
                    quote.Username,
                    quote.Content,
                    quote.InsertedAtUtc,
                    quote.Approved,
                    quote.Removed,
                    scores.GetValueOrDefault(quote.Id)))
        ];
        int scoreReceived = scores.Values.Sum();

        return new DashboardUserQuotePerformance(
            quotes.Count,
            quotes.Count(quote => quote.Approved && !quote.Removed),
            quotes.Count(quote => !quote.Approved && !quote.Removed),
            quotes.Count(quote => quote.Removed),
            scoreReceived,
            votesGiven,
            quotes.Count == 0 ? 0.0 : Math.Round((double)scoreReceived / quotes.Count, 1),
            scoreByServer,
            recentQuotes);
    }

    private async Task<DashboardUserQuoteTotals> BuildUserQuoteTotalsAsync(
        int userId,
        int? guildId,
        CancellationToken cancellationToken)
    {
        IQueryable<Quote> query = dbContext.Quotes
            .AsNoTracking()
            .Where(quote => quote.UserId == userId);
        if (guildId.HasValue)
            query = query.Where(quote => quote.GuildId == guildId.Value);

        List<int> quoteIds = await query.Select(quote => quote.Id).ToListAsync(cancellationToken);
        int scoreReceived = quoteIds.Count == 0
            ? 0
            : await dbContext.QuoteScores
                .AsNoTracking()
                .Where(score => quoteIds.Contains(score.QuoteId))
                .SumAsync(score => (int?)score.Score, cancellationToken) ?? 0;
        int votesGiven = await CountUserQuoteVotesGivenAsync(userId, guildId, null, null, cancellationToken);

        return new DashboardUserQuoteTotals(quoteIds.Count, scoreReceived, votesGiven);
    }

    private async Task<int> CountUserQuoteVotesGivenAsync(
        int userId,
        int? guildId,
        DateTime? startDate,
        DateTime? endExclusiveDate,
        CancellationToken cancellationToken)
    {
        IQueryable<QuoteScore> query = dbContext.QuoteScores
            .AsNoTracking()
            .Where(score => score.UserId == userId);

        if (startDate.HasValue)
            query = query.Where(score => score.InsertDate >= startDate.Value);

        if (endExclusiveDate.HasValue)
            query = query.Where(score => score.InsertDate < endExclusiveDate.Value);

        if (!guildId.HasValue)
            return await query.CountAsync(cancellationToken);

        return await query
            .Join(
                dbContext.Quotes.AsNoTracking().Where(quote => quote.GuildId == guildId.Value),
                score => score.QuoteId,
                quote => quote.Id,
                (score, _) => score)
            .CountAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<DashboardUserStockHolding>> BuildUserStockHoldingsAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.StockHoldings
            .AsNoTracking()
            .Where(holding => holding.UserId == userId)
            .Join(
                dbContext.Stocks.AsNoTracking(),
                holding => holding.StockId,
                stock => stock.Id,
                (holding, stock) => new
                {
                    StockId = stock.Id,
                    stock.EntityType,
                    stock.EntityId,
                    holding.Shares,
                    stock.Price,
                    holding.TotalInvested,
                    stock.DailyChangePercent
                })
            .ToListAsync(cancellationToken);

        List<StockInsightRow> stockRows =
        [
            .. rows.Select(row => new StockInsightRow(
                row.StockId,
                row.EntityType,
                row.EntityId,
                row.Price,
                row.DailyChangePercent))
        ];
        Dictionary<int, string> stockNames = await GetStockNamesAsync(stockRows, cancellationToken);

        return
        [
            .. rows.Select(row =>
            {
                decimal value = row.Shares * row.Price;
                return new DashboardUserStockHolding(
                    row.StockId,
                    EntityTypeLabel(row.EntityType),
                    stockNames.GetValueOrDefault(row.StockId, $"Stock #{row.StockId}"),
                    row.Shares,
                    row.Price,
                    value,
                    row.TotalInvested,
                    value - row.TotalInvested,
                    row.DailyChangePercent);
            })
            .OrderByDescending(holding => holding.Value)
        ];
    }

    private async Task<DashboardUserEconomyPerformance> BuildUserEconomyPerformanceAsync(
        int userId,
        decimal balance,
        IReadOnlyList<DashboardUserStockHolding> stockHoldings,
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        CancellationToken cancellationToken)
    {
        List<UserTransactionRaw> transactions = await dbContext.StockTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.InsertDate >= startDate &&
                transaction.InsertDate < endExclusiveDate &&
                (transaction.UserId == userId || transaction.TargetUserId == userId))
            .Select(transaction => new UserTransactionRaw(
                transaction.Id,
                transaction.UserId,
                transaction.TargetUserId,
                transaction.StockId,
                transaction.Type,
                transaction.Amount,
                transaction.Fee,
                transaction.InsertDate))
            .ToListAsync(cancellationToken);
        List<ScopedTransactionInsightRow> scopedTransactions =
        [
            .. transactions.Select(transaction => new ScopedTransactionInsightRow(
                new TransactionInsightRow(
                    transaction.UserId,
                    transaction.TargetUserId,
                    transaction.Type,
                    transaction.Amount,
                    transaction.Fee,
                    transaction.InsertDate),
                GetTransactionPerspective(
                    new TransactionInsightRow(
                        transaction.UserId,
                        transaction.TargetUserId,
                        transaction.Type,
                        transaction.Amount,
                        transaction.Fee,
                        transaction.InsertDate),
                    [userId])))
        ];
        List<DashboardUserTransactionItem> transactionItems = await BuildUserTransactionItemsAsync(
            transactions,
            userId,
            cancellationToken);
        decimal portfolioValue = stockHoldings.Sum(holding => holding.Value);
        decimal unrealizedGains = stockHoldings.Sum(holding => holding.UnrealizedGain);
        decimal realizedGains = scopedTransactions
            .Where(transaction => transaction.Perspective == TransactionPerspective.Actor)
            .Where(transaction => transaction.Transaction.Type is TransactionType.StockBuy or TransactionType.StockSell)
            .Sum(transaction => transaction.Transaction.Type == TransactionType.StockSell
                ? Math.Abs(transaction.Transaction.Amount)
                : -Math.Abs(transaction.Transaction.Amount));
        DashboardUserDonationStats donations = BuildUserDonationStats(transactionItems);
        DashboardUserOutcomeStats robbery = BuildUserOutcomeStats(
            scopedTransactions,
            transactionItems,
            TransactionType.RobberyWin,
            TransactionType.RobberyLoss);
        DashboardUserOutcomeStats slots = BuildUserOutcomeStats(
            scopedTransactions,
            transactionItems,
            TransactionType.SlotsWin,
            TransactionType.SlotsLoss);

        IReadOnlyList<DashboardCategoryValue> transactionTypes =
        [
            .. transactions
                .GroupBy(transaction => TransactionTypeLabel(transaction.Type))
                .Select(group => new DashboardCategoryValue(
                    group.Key,
                    group.Sum(transaction => Math.Abs(transaction.Amount))))
                .OrderByDescending(item => item.Value)
        ];

        return new DashboardUserEconomyPerformance(
            balance,
            portfolioValue,
            balance + portfolioValue,
            transactions.Sum(transaction => Math.Abs(transaction.Amount)),
            scopedTransactions.Sum(transaction => transaction.Perspective == TransactionPerspective.Target
                ? 0m
                : transaction.Transaction.Fee),
            realizedGains,
            unrealizedGains,
            transactions.Count(transaction => transaction.Type is TransactionType.StockBuy or TransactionType.StockSell or TransactionType.StockTransfer),
            donations,
            robbery,
            slots,
            BuildEconomyDailyFlow(scopedTransactions, startDate, days),
            transactionTypes,
            [.. transactionItems.OrderByDescending(item => item.InsertedAtUtc).Take(12)],
            [.. transactionItems
                .Where(item => item.Type is "Stock buy" or "Stock sell" or "Stock transfer")
                .OrderByDescending(item => item.InsertedAtUtc)
                .Take(12)]);
    }

    private async Task<List<DashboardUserTransactionItem>> BuildUserTransactionItemsAsync(
        IReadOnlyList<UserTransactionRaw> transactions,
        int userId,
        CancellationToken cancellationToken)
    {
        List<int> stockIds = [.. transactions
            .Where(transaction => transaction.StockId.HasValue)
            .Select(transaction => transaction.StockId!.Value)
            .Distinct()];
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
        List<int> counterpartyIds =
        [
            .. transactions
                .Select(transaction => transaction.UserId == userId
                    ? transaction.TargetUserId
                    : transaction.UserId)
                .Where(id => id.HasValue && id.Value != userId)
                .Select(id => id!.Value)
                .Distinct()
        ];
        Dictionary<int, (string DiscordId, string Username)> userLabels = await GetUserLabelsAsync(
            counterpartyIds,
            cancellationToken);

        return
        [
            .. transactions.Select(transaction =>
            {
                int? counterpartyId = transaction.UserId == userId
                    ? transaction.TargetUserId
                    : transaction.UserId;
                if (counterpartyId == userId)
                    counterpartyId = null;
                TransactionInsightRow insight = new(
                    transaction.UserId,
                    transaction.TargetUserId,
                    transaction.Type,
                    transaction.Amount,
                    transaction.Fee,
                    transaction.InsertDate);
                TransactionPerspective perspective = GetTransactionPerspective(insight, [userId]);
                string direction = perspective == TransactionPerspective.Internal
                    ? "Internal"
                    : IsTransactionInflow(transaction.Type, perspective)
                        ? "Incoming"
                        : IsTransactionOutflow(transaction.Type, perspective)
                            ? "Outgoing"
                            : "Neutral";

                return new DashboardUserTransactionItem(
                    transaction.Id,
                    TransactionTypeLabel(transaction.Type),
                    transaction.Amount,
                    perspective == TransactionPerspective.Target ? 0m : transaction.Fee,
                    direction,
                    counterpartyId,
                    counterpartyId.HasValue
                        ? userLabels.GetValueOrDefault(counterpartyId.Value, (string.Empty, $"User #{counterpartyId.Value}")).Item2
                        : null,
                    transaction.StockId,
                    transaction.StockId.HasValue
                        ? stockNames.GetValueOrDefault(transaction.StockId.Value, $"Stock #{transaction.StockId.Value}")
                        : null,
                    transaction.InsertDate);
            })
        ];
    }

    private static DashboardUserDonationStats BuildUserDonationStats(
        IReadOnlyList<DashboardUserTransactionItem> transactionItems)
    {
        IReadOnlyList<DashboardUserTransactionItem> donations =
        [
            .. transactionItems
                .Where(item => item.Type == "Donation")
                .OrderByDescending(item => item.InsertedAtUtc)
        ];

        return new DashboardUserDonationStats(
            donations.Count,
            donations.Sum(item => Math.Abs(item.Amount)),
            [.. donations.Take(6)]);
    }

    private static DashboardUserOutcomeStats BuildUserOutcomeStats(
        IReadOnlyList<ScopedTransactionInsightRow> scopedTransactions,
        IReadOnlyList<DashboardUserTransactionItem> transactionItems,
        TransactionType winType,
        TransactionType lossType)
    {
        string winLabel = TransactionTypeLabel(winType);
        string lossLabel = TransactionTypeLabel(lossType);
        var related = scopedTransactions
            .Where(row => row.Transaction.Type == winType || row.Transaction.Type == lossType)
            .ToList();
        decimal won = related
            .Where(row => IsTransactionInflow(row.Transaction.Type, row.Perspective))
            .Sum(row => Math.Abs(row.Transaction.Amount));
        decimal lost = related
            .Where(row => IsTransactionOutflow(row.Transaction.Type, row.Perspective))
            .Sum(row => Math.Abs(row.Transaction.Amount));
        IReadOnlyList<DashboardUserTransactionItem> recent =
        [
            .. transactionItems
                .Where(item => item.Type == winLabel || item.Type == lossLabel)
                .OrderByDescending(item => item.InsertedAtUtc)
                .Take(6)
        ];

        return new DashboardUserOutcomeStats(
            related.Count(row => IsTransactionInflow(row.Transaction.Type, row.Perspective)),
            related.Count(row => IsTransactionOutflow(row.Transaction.Type, row.Perspective)),
            won,
            lost,
            won - lost,
            recent);
    }

    private async Task<DashboardUserButtonGamePerformance> BuildUserButtonGamePerformanceAsync(
        int userId,
        int? guildId,
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        CancellationToken cancellationToken)
    {
        IQueryable<ButtonGamePress> query = dbContext.ButtonGamePresses
            .AsNoTracking()
            .Where(press =>
                press.UserId == userId &&
                press.InsertDate >= startDate &&
                press.InsertDate < endExclusiveDate);

        if (guildId.HasValue)
            query = query.Where(press => press.GuildId == guildId.Value);

        var rows = await query
            .Select(press => new
            {
                press.Id,
                press.GuildId,
                press.Score,
                press.InsertDate
            })
            .OrderBy(press => press.InsertDate)
            .ToListAsync(cancellationToken);
        List<int> buttonGuildIds =
        [
            .. rows
                .Where(row => row.GuildId.HasValue)
                .Select(row => row.GuildId!.Value)
                .Distinct()
        ];
        Dictionary<int, string> guildLabels = buttonGuildIds.Count > 0
            ? await dbContext.Guilds
                .AsNoTracking()
                .Where(guild => buttonGuildIds.Contains(guild.Id))
                .Select(guild => new { guild.Id, guild.Name })
                .ToDictionaryAsync(guild => guild.Id, guild => guild.Name, cancellationToken)
            : [];
        long cumulativeScore = 0L;
        IReadOnlyList<DashboardUserButtonScorePoint> timeline =
        [
            .. rows.Select(row =>
            {
                cumulativeScore += row.Score;
                return new DashboardUserButtonScorePoint(
                    row.InsertDate,
                    row.Score,
                    cumulativeScore,
                    row.GuildId.HasValue
                        ? guildLabels.GetValueOrDefault(row.GuildId.Value, $"Server #{row.GuildId.Value}")
                        : null);
            })
            .TakeLast(120)
        ];
        List<ButtonGameInsightRow> insightRows =
        [
            .. rows.Select(row => new ButtonGameInsightRow(row.Id, userId, row.GuildId, row.Score, row.InsertDate))
        ];
        long score = rows.Sum(row => row.Score);

        return new DashboardUserButtonGamePerformance(
            rows.Count,
            score,
            rows.Count == 0 ? 0.0 : Math.Round((double)score / rows.Count, 1),
            rows.Count == 0 ? 0L : rows.Max(row => row.Score),
            rows.Count == 0 ? null : rows.Max(row => row.InsertDate),
            BuildButtonGameDaily(insightRows, startDate, days),
            timeline);
    }

    private async Task<IReadOnlyList<DashboardUserReminderTimelineItem>> BuildUserReminderTimelineAsync(
        int userId,
        int? guildId,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        IQueryable<Reminder> query = dbContext.Reminders
            .AsNoTracking()
            .Where(reminder => reminder.UserId == userId);

        if (guildId.HasValue)
            query = query.Where(reminder => reminder.GuildId == guildId.Value);

        var rows = await query
            .Select(reminder => new
            {
                reminder.Id,
                reminder.GuildId,
                reminder.ChannelId,
                reminder.Text,
                reminder.InsertDate,
                reminder.DueDate,
                GuildName = reminder.Guild == null ? null : reminder.Guild.Name
            })
            .OrderBy(reminder => reminder.DueDate)
            .Take(20)
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new DashboardUserReminderTimelineItem(
                row.Id,
                row.GuildName ?? (row.GuildId.HasValue ? $"Server #{row.GuildId}" : "Global"),
                row.ChannelId.ToString(),
                row.Text,
                row.InsertDate,
                row.DueDate,
                row.DueDate < now))
        ];
    }

    private async Task<(DashboardUserRankSnapshot GlobalRank, IReadOnlyList<DashboardUserRankSnapshot> ServerRanks)> BuildUserRankSnapshotsAsync(
        int userId,
        IReadOnlyList<DashboardUserServerLevel> serverLevels,
        CancellationToken cancellationToken)
    {
        var globalRows = await dbContext.UserLevels
            .AsNoTracking()
            .GroupBy(level => level.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Xp = group.Sum(level => (long)level.TotalXp),
                Messages = group.Sum(level => (long)level.UserMessageCount)
            })
            .OrderByDescending(row => row.Xp)
            .ThenByDescending(row => row.Messages)
            .ThenBy(row => row.UserId)
            .ToListAsync(cancellationToken);
        int globalIndex = globalRows.FindIndex(row => row.UserId == userId);
        var userGlobal = globalIndex >= 0 ? globalRows[globalIndex] : null;
        DashboardUserRankSnapshot globalRank = new(
            null,
            "Global",
            globalIndex >= 0 ? globalIndex + 1 : null,
            globalRows.Count,
            userGlobal?.Xp ?? serverLevels.Sum(level => level.TotalXp),
            userGlobal?.Messages ?? serverLevels.Sum(level => level.Messages));
        IReadOnlyList<DashboardUserRankSnapshot> serverRanks =
        [
            .. serverLevels.Select(level => new DashboardUserRankSnapshot(
                level.GuildId,
                level.Name,
                level.Rank,
                level.RankPopulation,
                level.TotalXp,
                level.Messages))
        ];

        return (globalRank, serverRanks);
    }

    private async Task<IReadOnlyList<DashboardUserRankTimelinePoint>> BuildUserRankTimelineAsync(
        int userId,
        int? guildId,
        DateTime startDate,
        DateTime endExclusiveDate,
        int days,
        CancellationToken cancellationToken)
    {
        async Task<List<UserRankActivityRow>> LoadRowsAsync(int? scopedGuildId)
        {
            IQueryable<UserActivity> query = dbContext.UserActivity
                .AsNoTracking()
                .Where(activity => activity.InsertDate >= startDate && activity.InsertDate < endExclusiveDate);

            if (scopedGuildId.HasValue)
                query = query.Where(activity => activity.GuildId == scopedGuildId.Value);

            return await query
                .GroupBy(activity => new { Date = activity.InsertDate.Date, activity.UserId })
                .Select(group => new UserRankActivityRow(
                    group.Key.Date,
                    group.Key.UserId,
                    group.Sum(activity => (long)activity.XpGained)))
                .ToListAsync(cancellationToken);
        }

        List<UserRankActivityRow> globalRows = await LoadRowsAsync(null);
        List<UserRankActivityRow> serverRows = guildId.HasValue
            ? await LoadRowsAsync(guildId.Value)
            : [];
        Dictionary<int, long> globalCumulative = [];
        Dictionary<int, long> serverCumulative = [];
        List<DashboardUserRankTimelinePoint> points = [];

        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset).Date;
            AddDailyXp(globalCumulative, globalRows.Where(row => row.Date == date));
            AddDailyXp(serverCumulative, serverRows.Where(row => row.Date == date));
            long userGlobalXp = globalCumulative.GetValueOrDefault(userId);
            long userServerXp = serverCumulative.GetValueOrDefault(userId);
            int? globalRank = userGlobalXp > 0L
                ? globalCumulative.Count(row => row.Value > userGlobalXp) + 1
                : null;
            int? serverRank = guildId.HasValue && userServerXp > 0L
                ? serverCumulative.Count(row => row.Value > userServerXp) + 1
                : null;
            long leadingXp = Math.Max(
                globalCumulative.Count == 0 ? 0L : globalCumulative.Values.Max(),
                serverCumulative.Count == 0 ? 0L : serverCumulative.Values.Max());

            points.Add(new DashboardUserRankTimelinePoint(
                DateTime.SpecifyKind(date, DateTimeKind.Utc),
                globalRank,
                serverRank,
                guildId.HasValue ? userServerXp : userGlobalXp,
                leadingXp));
        }

        return points;

        static void AddDailyXp(Dictionary<int, long> cumulative, IEnumerable<UserRankActivityRow> rows)
        {
            foreach (UserRankActivityRow row in rows)
                cumulative[row.UserId] = cumulative.GetValueOrDefault(row.UserId) + row.Xp;
        }
    }

    private async Task<int> GetUserKnownChannelCountAsync(
        int userId,
        int? guildId,
        CancellationToken cancellationToken)
    {
        IQueryable<UserActivity> query = dbContext.UserActivity
            .AsNoTracking()
            .Where(activity => activity.UserId == userId);

        if (guildId.HasValue)
            query = query.Where(activity => activity.GuildId == guildId.Value);

        return await query
            .Select(activity => activity.DiscordChannelId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private async Task<DateTime?> GetUserLastActivityAsync(
        int userId,
        int? guildId,
        CancellationToken cancellationToken)
    {
        IQueryable<UserActivity> query = dbContext.UserActivity
            .AsNoTracking()
            .Where(activity => activity.UserId == userId);

        if (guildId.HasValue)
            query = query.Where(activity => activity.GuildId == guildId.Value);

        return await query
            .Select(activity => (DateTime?)activity.InsertDate)
            .MaxAsync(cancellationToken);
    }

    private static double WeightedAverage(
        IReadOnlyList<DashboardUserServerLevel> rows,
        Func<DashboardUserServerLevel, long> weightSelector,
        Func<DashboardUserServerLevel, double> valueSelector)
    {
        long totalWeight = rows.Sum(weightSelector);
        return totalWeight == 0L
            ? 0.0
            : rows.Sum(row => valueSelector(row) * weightSelector(row)) / totalWeight;
    }

    private static IReadOnlyList<DashboardUserLevelPoint> BuildUserLevelProgression(
        IReadOnlyList<DashboardActivityDerivedPoint> activityPoints,
        long currentTotalXp)
    {
        long windowXp = activityPoints.Count == 0 ? 0L : activityPoints[^1].CumulativeXp;
        long baselineXp = Math.Max(0L, currentTotalXp - windowXp);

        return
        [
            .. activityPoints.Select(point =>
            {
                long totalXp = baselineXp + point.CumulativeXp;
                return new DashboardUserLevelPoint(
                    point.DateUtc,
                    totalXp,
                    ActivityLevelService.CalculateLevel(totalXp));
            })
        ];
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

    private async Task<Dictionary<int, string>> GetGuildLabelsAsync(
        IEnumerable<int> guildIds,
        CancellationToken cancellationToken)
    {
        List<int> ids = [.. guildIds.Distinct()];
        if (ids.Count == 0)
            return [];

        var guilds = await dbContext.Guilds
            .AsNoTracking()
            .Where(guild => ids.Contains(guild.Id))
            .Select(guild => new { guild.Id, guild.Name })
            .ToListAsync(cancellationToken);

        return guilds.ToDictionary(
            guild => guild.Id,
            guild => string.IsNullOrWhiteSpace(guild.Name) ? $"Server #{guild.Id}" : guild.Name);
    }

    private async Task<Dictionary<int, long>> GetTotalXpByUserAsync(
        IEnumerable<int> userIds,
        int? guildId,
        int? selectedUserId,
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
        if (selectedUserId.HasValue)
            query = query.Where(levels => levels.UserId == selectedUserId.Value);

        var rows = await query
            .GroupBy(levels => levels.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                TotalXp = group.Sum(levels => (long)levels.TotalXp)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.UserId, row => row.TotalXp);
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

    private static string TrimForDashboard(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return value;

        return string.Concat(value.AsSpan(0, Math.Max(0, maxLength - 1)), "...");
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

    private static List<DashboardButtonGameUser> BuildButtonGameUsers(
        IReadOnlyList<ButtonGameInsightRow> rows,
        IReadOnlyDictionary<int, (string DiscordId, string Username)> userLabels,
        Func<(int UserId, long Presses, long Score, DateTime LastPressAtUtc), decimal> orderBy,
        bool descending,
        int limit)
    {
        var users = rows
            .GroupBy(row => row.UserId)
            .Select(group => (
                UserId: group.Key,
                Presses: (long)group.Count(),
                Score: group.Sum(row => row.Score),
                LastPressAtUtc: group.Max(row => row.InsertDate)));

        users = descending
            ? users.OrderByDescending(orderBy).ThenByDescending(user => user.LastPressAtUtc)
            : users.OrderBy(orderBy).ThenByDescending(user => user.LastPressAtUtc);

        return
        [
            .. users
                .Take(limit)
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
    }

    private static IReadOnlyList<DashboardButtonGameScoreEntry> BuildButtonScoreEntries(
        IEnumerable<ButtonGameInsightRow> rows,
        IReadOnlyDictionary<int, (string DiscordId, string Username)> userLabels,
        IReadOnlyDictionary<int, string> guildLabels)
    {
        return
        [
            .. rows
                .Select((row, index) =>
                {
                    (string discordId, string username) = userLabels.GetValueOrDefault(row.UserId, (string.Empty, "Unknown"));
                    string? guildName = row.GuildId.HasValue
                        ? guildLabels.GetValueOrDefault(row.GuildId.Value, $"Server #{row.GuildId.Value}")
                        : null;
                    return new DashboardButtonGameScoreEntry(
                        index + 1,
                        row.PressId,
                        row.UserId,
                        discordId,
                        username,
                        row.GuildId,
                        guildName,
                        row.Score,
                        row.InsertDate);
                })
        ];
    }

    private static IReadOnlyList<DashboardHistogramBucket> BuildButtonScoreDistribution(IEnumerable<long> scores)
    {
        List<long> values = [.. scores.OrderBy(score => score)];
        if (values.Count == 0)
            return [];

        long min = values.First();
        long max = values.Last();
        if (min == max)
            return [new DashboardHistogramBucket($"{min:N0}", values.Count)];

        int bucketCount = Math.Min(8, Math.Max(3, (int)Math.Ceiling(Math.Sqrt(values.Count))));
        decimal width = Math.Max(1m, (decimal)(max - min + 1) / bucketCount);
        List<DashboardHistogramBucket> buckets = [];

        for (int index = 0; index < bucketCount; index++)
        {
            decimal start = min + width * index;
            decimal end = index == bucketCount - 1 ? max : min + width * (index + 1) - 1;
            int count = values.Count(score => score >= start && score <= end);
            buckets.Add(new DashboardHistogramBucket($"{Math.Floor(start):N0}-{Math.Floor(end):N0}", count));
        }

        return buckets;
    }

    private static IReadOnlyList<DashboardCategoryValue> BuildButtonPressesByServer(
        IReadOnlyList<ButtonGameInsightRow> rows,
        IReadOnlyDictionary<int, string> guildLabels)
    {
        return
        [
            .. rows
                .Where(row => row.GuildId.HasValue)
                .GroupBy(row => row.GuildId!.Value)
                .Select(group => new DashboardCategoryValue(
                    guildLabels.GetValueOrDefault(group.Key, $"Server #{group.Key}"),
                    group.Count()))
                .OrderByDescending(point => point.Value)
                .Take(12)
        ];
    }

    private static IReadOnlyList<DashboardButtonGameServer> BuildCompetitiveButtonServers(
        IReadOnlyList<ButtonGameInsightRow> rows,
        IReadOnlyDictionary<int, string> guildLabels)
    {
        return
        [
            .. rows
                .Where(row => row.GuildId.HasValue)
                .GroupBy(row => row.GuildId!.Value)
                .Select(group =>
                {
                    long presses = group.Count();
                    long score = group.Sum(row => row.Score);
                    int activeUsers = group.Select(row => row.UserId).Distinct().Count();
                    double averageScore = presses == 0 ? 0.0 : Math.Round((double)score / presses, 1);
                    double competitiveScore = Math.Round(presses * 0.45 + activeUsers * 8.0 + averageScore * 0.35, 1);
                    return new
                    {
                        GuildId = group.Key,
                        Presses = presses,
                        Score = score,
                        ActiveUsers = activeUsers,
                        AverageScore = averageScore,
                        CompetitiveScore = competitiveScore,
                        LastPressAtUtc = group.Max(row => row.InsertDate)
                    };
                })
                .OrderByDescending(server => server.CompetitiveScore)
                .Take(12)
                .Select((server, index) => new DashboardButtonGameServer(
                    index + 1,
                    server.GuildId,
                    guildLabels.GetValueOrDefault(server.GuildId, $"Server #{server.GuildId}"),
                    server.Presses,
                    server.Score,
                    server.ActiveUsers,
                    server.AverageScore,
                    server.CompetitiveScore,
                    server.LastPressAtUtc))
        ];
    }

    private static IReadOnlyList<DashboardCalendarActivityCell> BuildButtonCalendarHeatmap(
        IReadOnlyList<ButtonGameInsightRow> rows,
        DateTime startDate,
        int days)
    {
        Dictionary<DateTime, List<ButtonGameInsightRow>> byDate = rows
            .GroupBy(row => row.InsertDate.Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        return
        [
            .. Enumerable.Range(0, days)
                .Select(offset =>
                {
                    DateTime date = startDate.AddDays(offset).Date;
                    List<ButtonGameInsightRow> dayRows = byDate.GetValueOrDefault(date, []);
                    return new DashboardCalendarActivityCell(
                        DateTime.SpecifyKind(date, DateTimeKind.Utc),
                        dayRows.Count,
                        dayRows.Sum(row => row.Score),
                        dayRows.Select(row => row.UserId).Distinct().Count());
                })
        ];
    }

    private static IReadOnlyList<DashboardHeatmapCell> BuildButtonHourByWeekdayHeatmap(IReadOnlyList<ButtonGameInsightRow> rows)
    {
        Dictionary<(int Day, int Hour), List<ButtonGameInsightRow>> lookup = rows
            .GroupBy(row => ((int)row.InsertDate.DayOfWeek, row.InsertDate.Hour))
            .ToDictionary(group => group.Key, group => group.ToList());

        List<DashboardHeatmapCell> cells = [];
        for (int day = 0; day < 7; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                List<ButtonGameInsightRow> hourRows = lookup.GetValueOrDefault((day, hour), []);
                cells.Add(new DashboardHeatmapCell(
                    day,
                    DayLabels[day],
                    hour,
                    hourRows.Count,
                    hourRows.Sum(row => row.Score),
                    hourRows.Select(row => row.UserId).Distinct().Count()));
            }
        }

        return cells;
    }

    private static IReadOnlyList<DashboardButtonGameGap> BuildButtonGameGaps(IReadOnlyList<ButtonGameInsightRow> rows)
    {
        List<ButtonGameInsightRow> ordered = [.. rows.OrderBy(row => row.InsertDate)];
        if (ordered.Count < 2)
            return [];

        return
        [
            .. ordered
                .Zip(ordered.Skip(1), (previous, next) => new
                {
                    StartedAtUtc = previous.InsertDate,
                    EndedAtUtc = next.InsertDate,
                    Hours = (next.InsertDate - previous.InsertDate).TotalHours,
                    PreviousScore = previous.Score,
                    NextScore = next.Score
                })
                .OrderByDescending(gap => gap.Hours)
                .Take(10)
                .Select((gap, index) => new DashboardButtonGameGap(
                    index + 1,
                    gap.StartedAtUtc,
                    gap.EndedAtUtc,
                    Math.Round(gap.Hours, 1),
                    gap.PreviousScore,
                    gap.NextScore))
        ];
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

        List<ReminderInsightRow> reminders = await query
            .Select(reminder => new
            {
                reminder.Id,
                reminder.GuildId,
                GuildName = reminder.Guild == null ? null : reminder.Guild.Name,
                reminder.ChannelId,
                reminder.Text,
                reminder.InsertDate,
                reminder.DueDate,
                reminder.UserId,
                Username = reminder.User == null ? "Unknown" : reminder.User.Username
            })
            .Select(reminder => new ReminderInsightRow(
                reminder.Id,
                reminder.GuildId,
                reminder.GuildName,
                reminder.ChannelId,
                reminder.UserId,
                reminder.Username,
                reminder.Text,
                reminder.InsertDate,
                reminder.DueDate))
            .ToListAsync(cancellationToken);
        Dictionary<ulong, string> channelLabels = await GetChannelLabelsAsync(
            reminders.Select(reminder => reminder.ChannelId),
            cancellationToken);

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
                    reminder.GuildName,
                    reminder.InsertDate,
                    reminder.DueDate,
                    reminder.DueDate < now))
        ];
        double averageLeadTimeHours = reminders.Count == 0
            ? 0.0
            : Math.Round(reminders.Average(reminder => Math.Max(0.0, (reminder.DueDate - reminder.InsertDate).TotalHours)), 1);
        DateTime startDate = reminders.Count == 0
            ? now.Date
            : reminders.Min(reminder => reminder.InsertDate.Date);
        DateTime endDate = reminders.Count == 0
            ? now.Date
            : reminders.Max(reminder => reminder.DueDate.Date);
        int days = Math.Clamp((int)(endDate - startDate).TotalDays + 1, 1, 3650);

        return new DashboardReminderStats(
            reminders.Count,
            reminders.Count(reminder => reminder.DueDate < now),
            reminders.Count(reminder => reminder.DueDate >= now && reminder.DueDate <= now.AddDays(1)),
            averageLeadTimeHours,
            upcoming,
            BuildReminderCategoryBreakdown(
                reminders,
                reminder => reminder.GuildId.HasValue
                    ? (reminder.GuildName ?? $"Server #{reminder.GuildId.Value}")
                    : "No server"),
            BuildReminderCategoryBreakdown(reminders, reminder => reminder.Username),
            BuildReminderCategoryBreakdown(
                reminders,
                reminder => channelLabels.GetValueOrDefault(reminder.ChannelId, $"channel-{ShortDiscordId(reminder.ChannelId)}")),
            BuildReminderTimeline(reminders, startDate, days, now),
            BuildReminderDueTimeline(reminders, startDate, days, now),
            BuildReminderCalendar(reminders, startDate, days));
    }

    private static IReadOnlyList<DashboardCategoryValue> BuildReminderCategoryBreakdown(
        IReadOnlyList<ReminderInsightRow> reminders,
        Func<ReminderInsightRow, string> labelSelector)
    {
        return
        [
            .. reminders
                .GroupBy(labelSelector)
                .Select(group => new DashboardCategoryValue(
                    string.IsNullOrWhiteSpace(group.Key) ? "Unknown" : group.Key,
                    group.Count()))
                .OrderByDescending(point => point.Value)
                .ThenBy(point => point.Label)
                .Take(12)
        ];
    }

    private static IReadOnlyList<DashboardReminderPoint> BuildReminderTimeline(
        IReadOnlyList<ReminderInsightRow> reminders,
        DateTime startDate,
        int days,
        DateTime now)
    {
        Dictionary<DateTime, int> created = reminders
            .GroupBy(reminder => reminder.InsertDate.Date)
            .ToDictionary(group => group.Key, group => group.Count());
        Dictionary<DateTime, int> due = reminders
            .GroupBy(reminder => reminder.DueDate.Date)
            .ToDictionary(group => group.Key, group => group.Count());

        return
        [
            .. Enumerable.Range(0, days)
                .Select(offset =>
                {
                    DateTime date = startDate.AddDays(offset).Date;
                    int dueCount = due.GetValueOrDefault(date);
                    int overdue = date < now.Date ? dueCount : 0;
                    int upcoming = date >= now.Date ? dueCount : 0;
                    return new DashboardReminderPoint(
                        DateTime.SpecifyKind(date, DateTimeKind.Utc),
                        created.GetValueOrDefault(date),
                        dueCount,
                        overdue,
                        upcoming);
                })
        ];
    }

    private static IReadOnlyList<DashboardReminderPoint> BuildReminderDueTimeline(
        IReadOnlyList<ReminderInsightRow> reminders,
        DateTime startDate,
        int days,
        DateTime now) =>
        BuildReminderTimeline(reminders, startDate, days, now)
            .Where(point => point.Due > 0 || point.Created > 0)
            .OrderBy(point => point.DateUtc)
            .TakeLast(90)
            .ToList();

    private static IReadOnlyList<DashboardCalendarActivityCell> BuildReminderCalendar(
        IReadOnlyList<ReminderInsightRow> reminders,
        DateTime startDate,
        int days)
    {
        Dictionary<DateTime, List<ReminderInsightRow>> byDueDate = reminders
            .GroupBy(reminder => reminder.DueDate.Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        return
        [
            .. Enumerable.Range(0, days)
                .Select(offset =>
                {
                    DateTime date = startDate.AddDays(offset).Date;
                    List<ReminderInsightRow> dayRows = byDueDate.GetValueOrDefault(date, []);
                    return new DashboardCalendarActivityCell(
                        DateTime.SpecifyKind(date, DateTimeKind.Utc),
                        dayRows.Count,
                        0,
                        dayRows.Select(row => row.UserId).Where(id => id.HasValue).Distinct().Count());
                })
        ];
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

        List<TemporaryBan> allBans = await query
            .Where(ban => ban.InsertDate >= now.AddDays(-90) || ban.ExpiresAt >= now.AddDays(-90) || ban.UnbannedAt >= now.AddDays(-90))
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
                    ban.InsertDate,
                    ban.ExpiresAt,
                    ban.UnbannedAt,
                    ban.ExpiresAt < now ? "Overdue" : "Pending"))
        ];
        (int reactionRoleMessages, int reactionRoleItems, IReadOnlyList<DashboardCategoryValue> reactionRoleTypes, IReadOnlyList<DashboardReactionRoleUsage> reactionRoleUsage) =
            await BuildReactionRoleInsightsAsync(guildId, cancellationToken);
        IReadOnlyList<DashboardCategoryValue> activityRoleDistribution = await BuildConfiguredActivityRoleDistributionAsync(guildId, cancellationToken);
        (IReadOnlyList<DashboardServerConfigurationScorecard> scorecards, IReadOnlyList<DashboardServerSetupIssue> incompleteSetup, IReadOnlyList<DashboardServerSetupIssue> riskyConfiguration) =
            await BuildConfigurationModerationInsightsAsync(guildId, cancellationToken);

        return new DashboardModerationStats(
            pendingBans.Count,
            pendingBans.Count(ban => ban.ExpiresAt < now),
            completedLast30Days,
            reactionRoleMessages,
            reactionRoleItems,
            pending,
            BuildTemporaryBanTimeline(allBans, now.AddDays(-29).Date, 30, now),
            BuildTemporaryBanStatus(pendingBans.Count, pendingBans.Count(ban => ban.ExpiresAt < now), completedLast30Days),
            BuildBanReasonBreakdown(allBans),
            reactionRoleTypes,
            reactionRoleUsage,
            activityRoleDistribution,
            scorecards,
            incompleteSetup,
            riskyConfiguration);
    }

    private async Task<(int Messages, int Items, IReadOnlyList<DashboardCategoryValue> Types, IReadOnlyList<DashboardReactionRoleUsage> Usage)>
        BuildReactionRoleInsightsAsync(int? guildId, CancellationToken cancellationToken)
    {
        IQueryable<ReactionRoleMessage> query = dbContext.ReactionRoleMessages.AsNoTracking();
        if (guildId.HasValue)
            query = query.Where(message => message.GuildId == guildId.Value);

        var rows = await query
            .Select(message => new
            {
                message.GuildId,
                GuildName = message.Guild.Name,
                message.UseButtons,
                Items = message.Items.Count
            })
            .ToListAsync(cancellationToken);

        int messages = rows.Count;
        int items = rows.Sum(row => row.Items);
        IReadOnlyList<DashboardCategoryValue> types =
        [
            new DashboardCategoryValue("Button messages", rows.Count(row => row.UseButtons)),
            new DashboardCategoryValue("Emoji messages", rows.Count(row => !row.UseButtons)),
            new DashboardCategoryValue("Button items", rows.Where(row => row.UseButtons).Sum(row => row.Items)),
            new DashboardCategoryValue("Emoji items", rows.Where(row => !row.UseButtons).Sum(row => row.Items))
        ];
        IReadOnlyList<DashboardReactionRoleUsage> usage =
        [
            .. rows
                .GroupBy(row => new { row.GuildId, row.GuildName })
                .Select(group => new DashboardReactionRoleUsage(
                    group.Key.GuildId,
                    string.IsNullOrWhiteSpace(group.Key.GuildName) ? $"Server #{group.Key.GuildId}" : group.Key.GuildName,
                    group.Count(),
                    group.Sum(row => row.Items),
                    group.Count(row => row.UseButtons),
                    group.Count(row => !row.UseButtons)))
                .OrderByDescending(row => row.Items)
                .ThenByDescending(row => row.Messages)
                .Take(12)
        ];

        return (messages, items, types, usage);
    }

    private async Task<IReadOnlyList<DashboardCategoryValue>> BuildConfiguredActivityRoleDistributionAsync(
        int? guildId,
        CancellationToken cancellationToken)
    {
        IQueryable<Role> query = dbContext.Roles.AsNoTracking();
        if (guildId.HasValue)
            query = query.Where(role => role.GuildId == guildId.Value);

        var rows = await query
            .GroupBy(role => role.RoleType)
            .Select(group => new { RoleType = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows
                .Select(row => new DashboardCategoryValue(row.RoleType.ToString(), row.Count))
                .OrderByDescending(point => point.Value)
        ];
    }

    private async Task<(IReadOnlyList<DashboardServerConfigurationScorecard> Scorecards, IReadOnlyList<DashboardServerSetupIssue> IncompleteSetup, IReadOnlyList<DashboardServerSetupIssue> RiskyConfiguration)>
        BuildConfigurationModerationInsightsAsync(int? guildId, CancellationToken cancellationToken)
    {
        IQueryable<Guild> query = dbContext.Guilds.AsNoTracking();
        if (guildId.HasValue)
            query = query.Where(guild => guild.Id == guildId.Value);

        List<Guild> guilds = await query.ToListAsync(cancellationToken);
        List<int> guildIds = [.. guilds.Select(guild => guild.Id)];
        Dictionary<int, int> activityRoleCounts = await dbContext.Roles
            .AsNoTracking()
            .Where(role => guildIds.Contains(role.GuildId))
            .GroupBy(role => role.GuildId)
            .Select(group => new { GuildId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.GuildId, row => row.Count, cancellationToken);

        List<DashboardServerConfigurationScorecard> scorecards = [];
        List<DashboardServerSetupIssue> incompleteSetup = [];
        List<DashboardServerSetupIssue> riskyConfiguration = [];

        foreach (Guild guild in guilds)
        {
            string guildName = string.IsNullOrWhiteSpace(guild.Name) ? $"Server #{guild.Id}" : guild.Name;
            List<(string Label, bool Passed, string Detail, bool Risky)> checks =
            [
                ("Welcome channel", !guild.WelcomeMessages || guild.WelcomeChannelId != 0, guild.WelcomeMessages ? "Welcome messages need a channel" : "Welcome messages disabled", false),
                ("Pins channel", guild.PinsChannelId != 0, guild.PinsChannelId == 0 ? "Pins channel is missing" : "Pins channel configured", false),
                ("Honeypot channel", !guild.SendHoneypotMessages || guild.HoneypotChannelId != 0, guild.SendHoneypotMessages ? "Honeypot messages need a channel" : "Honeypot messages disabled", true),
                ("Level-up message channel", !guild.LevelUpMessages || guild.LevelUpMessagesChannelId != 0, guild.LevelUpMessages ? "Level-up messages need a channel" : "Level-up messages disabled", false),
                ("Level-up quote channel", !guild.LevelUpQuotes || guild.LevelUpQuotesChannelId != 0, guild.LevelUpQuotes ? "Level-up quote messages need a channel" : "Level-up quote messages disabled", false),
                ("Quote approval channel", guild.QuotesApprovalChannelId != 0, guild.QuotesApprovalChannelId == 0 ? "Quote approvals have no channel" : "Quote approval channel configured", true),
                ("Quote add threshold", guild.QuoteAddRequiredApprovals >= 2, guild.QuoteAddRequiredApprovals < 2 ? "Add threshold is weak" : $"{guild.QuoteAddRequiredApprovals} approvals required", true),
                ("Quote remove threshold", guild.QuoteRemoveRequiredApprovals >= 2, guild.QuoteRemoveRequiredApprovals < 2 ? "Remove threshold is weak" : $"{guild.QuoteRemoveRequiredApprovals} approvals required", true),
                ("Activity roles", !guild.UseActivityRoles || activityRoleCounts.GetValueOrDefault(guild.Id) > 0, guild.UseActivityRoles ? $"{activityRoleCounts.GetValueOrDefault(guild.Id)} activity roles configured" : "Activity roles disabled", false)
            ];

            int passed = checks.Count(check => check.Passed);
            int failed = checks.Count - passed;
            int score = checks.Count == 0 ? 100 : ClampScore(passed / (double)checks.Count * 100.0);
            string risk = score >= 85
                ? "Strong"
                : score >= 70
                    ? "Watch"
                    : score >= 50
                        ? "Weak"
                        : "Risky";
            List<string> notes =
            [
                .. checks
                    .Where(check => !check.Passed)
                    .Take(4)
                    .Select(check => check.Label)
            ];
            if (notes.Count == 0)
                notes.Add("Core setup checks pass.");

            scorecards.Add(new DashboardServerConfigurationScorecard(
                guild.Id,
                guildName,
                score,
                risk,
                passed,
                failed,
                notes));

            foreach ((string label, bool passedCheck, string detail, bool risky) in checks.Where(check => !check.Passed))
            {
                DashboardServerSetupIssue issue = new(
                    guild.Id,
                    guildName,
                    risky ? "Risk" : "Missing",
                    label,
                    detail);
                if (risky)
                    riskyConfiguration.Add(issue);
                else
                    incompleteSetup.Add(issue);
            }
        }

        return (
            [.. scorecards.OrderBy(card => card.Score).ThenBy(card => card.GuildName).Take(16)],
            [.. incompleteSetup.OrderBy(issue => issue.GuildName).ThenBy(issue => issue.Label).Take(24)],
            [.. riskyConfiguration.OrderBy(issue => issue.GuildName).ThenBy(issue => issue.Label).Take(24)]);
    }

    private static IReadOnlyList<DashboardTemporaryBanPoint> BuildTemporaryBanTimeline(
        IReadOnlyList<TemporaryBan> bans,
        DateTime startDate,
        int days,
        DateTime now)
    {
        return
        [
            .. Enumerable.Range(0, days)
                .Select(offset =>
                {
                    DateTime date = startDate.AddDays(offset).Date;
                    return new DashboardTemporaryBanPoint(
                        DateTime.SpecifyKind(date, DateTimeKind.Utc),
                        bans.Count(ban => ban.InsertDate.Date == date),
                        bans.Count(ban => ban.UnbannedAt.HasValue && ban.UnbannedAt.Value.Date == date),
                        bans.Count(ban => ban.ExpiresAt.Date == date),
                        bans.Count(ban => ban.UnbannedAt == null && ban.ExpiresAt.Date == date && ban.ExpiresAt < now));
                })
        ];
    }

    private static IReadOnlyList<DashboardCategoryValue> BuildTemporaryBanStatus(
        int pending,
        int overdue,
        int completedLast30Days) =>
    [
        new DashboardCategoryValue("Pending", pending),
        new DashboardCategoryValue("Overdue", overdue),
        new DashboardCategoryValue("Completed 30d", completedLast30Days)
    ];

    private static IReadOnlyList<DashboardCategoryValue> BuildBanReasonBreakdown(IReadOnlyList<TemporaryBan> bans)
    {
        return
        [
            .. bans
                .GroupBy(ban => NormalizeReasonLabel(ban.Reason))
                .Select(group => new DashboardCategoryValue(group.Key, group.Count()))
                .OrderByDescending(reason => reason.Value)
                .ThenBy(reason => reason.Label)
                .Take(10)
        ];
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

    private static IReadOnlyList<DashboardCategoryValue> BuildLogHealthIndicators(
        int total,
        int warnings,
        int errors,
        int critical)
    {
        decimal severe = errors + critical;
        decimal severeRate = total == 0 ? 0m : Math.Round(severe / total * 100m, 1);
        decimal warningRate = total == 0 ? 0m : Math.Round(warnings / (decimal)total * 100m, 1);
        decimal healthyRate = total == 0 ? 100m : Math.Max(0m, 100m - severeRate - warningRate);

        return
        [
            new DashboardCategoryValue("Healthy log share", healthyRate),
            new DashboardCategoryValue("Warning rate", warningRate),
            new DashboardCategoryValue("Error/critical rate", severeRate),
            new DashboardCategoryValue("Incident count", severe)
        ];
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

    private static string NormalizeReasonLabel(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "No reason";

        string trimmed = reason.Trim();
        return trimmed.Length <= 48 ? trimmed : $"{trimmed[..45]}...";
    }

    private static string NormalizeLogMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Empty message";

        string trimmed = message.Trim();
        string normalized = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\d{4,}", "#");
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[0-9a-fA-F]{8,}", "#");
        return normalized.Length <= 72 ? normalized : $"{normalized[..69]}...";
    }

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
            "server" or "guild" => guildId.HasValue ? "server" : "global",
            "user" => userId.HasValue ? "user" : "global",
            "channel" => channelId.HasValue ? "channel" : "global",
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
        bool validChannelId = TryParseDiscordId(channelId, out ulong? channelDiscordId);
        if (!validChannelId)
            return new DashboardScopeFilters(guildId, null, 0UL, "channel");

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

    private static IReadOnlyList<DashboardActivityPoint> BuildEmptyActivityPoints(DateTime startDate, int days)
    {
        List<DashboardActivityPoint> points = [];
        for (int offset = 0; offset < days; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            points.Add(new DashboardActivityPoint(DateTime.SpecifyKind(date, DateTimeKind.Utc), 0, 0, 0, 0.0));
        }

        return points;
    }

    private static ulong? ParseDiscordId(string? value) =>
        TryParseDiscordId(value, out ulong? parsed) ? parsed : null;

    private static bool TryParseDiscordId(string? value, out ulong? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (ulong.TryParse(value.Trim(), out ulong id) && id > 0UL)
        {
            parsed = id;
            return true;
        }

        return false;
    }

    private static string ShortDiscordId(ulong id)
    {
        string value = id.ToString();
        return value.Length <= 4 ? value : value[^4..];
    }

    private static PeriodKey BuildWeekPeriodKey(DateTime date)
    {
        DateTime day = date.Date;
        int offset = ((int)day.DayOfWeek + 6) % 7;
        DateTime weekStart = day.AddDays(-offset);
        return new PeriodKey(
            weekStart.Year,
            System.Globalization.ISOWeek.GetWeekOfYear(weekStart),
            weekStart.DayOfYear,
            $"{weekStart:yyyy-MM-dd} week");
    }

    private static decimal Percentage(long value, long total) =>
        total <= 0L ? 0m : Math.Round((decimal)value / total * 100m, 1);

    private static DashboardActivityBoxPlotPoint CreateBoxPlot(
        string label,
        string kind,
        IReadOnlyList<int> values)
    {
        if (values.Count == 0)
            return new DashboardActivityBoxPlotPoint(label, kind, 0, 0, 0, 0, 0, 0, 0);

        List<int> sorted = [.. values.OrderBy(value => value)];
        return new DashboardActivityBoxPlotPoint(
            label,
            kind,
            sorted[0],
            Math.Round(Percentile(sorted, 0.25), 1),
            Math.Round(Percentile(sorted, 0.5), 1),
            Math.Round(Percentile(sorted, 0.75), 1),
            sorted[^1],
            Math.Round(sorted.Average(), 1),
            sorted.Count);
    }

    private static double Percentile(IReadOnlyList<int> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
            return 0.0;
        if (sortedValues.Count == 1)
            return sortedValues[0];

        double position = (sortedValues.Count - 1) * percentile;
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        if (lower == upper)
            return sortedValues[lower];

        double weight = position - lower;
        return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * weight;
    }

    private static int CalculateLongestStreak(IReadOnlyList<DateTime> dates)
    {
        if (dates.Count == 0)
            return 0;

        int longest = 1;
        int current = 1;
        for (int index = 1; index < dates.Count; index++)
        {
            if ((dates[index].Date - dates[index - 1].Date).Days == 1)
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 1;
            }
        }

        return longest;
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

    private sealed record PeriodKey(
        int Year,
        int Period,
        int SubPeriod,
        string Label);

    private sealed record EntityDailyActivityRow(
        string EntityId,
        DateTime Date,
        int Messages,
        long Xp,
        int ActiveUsers);

    private sealed record ActivityLeaderboardCandidate(
        string EntityId,
        int? UserId,
        string? Label,
        string EntityType,
        decimal Value,
        long Messages,
        long Xp,
        double AverageMessageLength,
        double XpPerMessage,
        DateTime? LastActivityAtUtc,
        double? DeltaPercent);

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

    private sealed record QuoteScoreInsightRow(
        int QuoteId,
        int UserId,
        string DiscordId,
        string Username,
        int Score,
        DateTime InsertedAtUtc,
        DateTime? UpdatedAtUtc);

    private sealed record QuoteScoreStats(
        int Total,
        int PositiveVotes,
        int NegativeVotes,
        int Votes,
        DateTime? LastVotedAtUtc)
    {
        public static QuoteScoreStats Empty { get; } = new(0, 0, 0, 0, null);
    }

    private sealed record QuoteApprovalInsightRow(
        int Id,
        int QuoteId,
        ulong ApprovalMessageId,
        int Score,
        DateTime InsertedAtUtc,
        QuoteApprovalType Type,
        bool Approved);

    private sealed record QuoteApprovalVoteInsightRow(
        int ApprovalId,
        ulong UserId,
        DateTime InsertedAtUtc);

    private sealed record GuildQuoteConfigRow(
        int GuildId,
        string DiscordId,
        string Name,
        bool UsesGlobalQuotes,
        bool ApprovalChannelConfigured,
        int AddRequiredApprovals,
        int RemoveRequiredApprovals)
    {
        public static GuildQuoteConfigRow Unknown(int guildId) =>
            new(guildId, guildId.ToString(), $"Server #{guildId}", false, false, 5, 5);
    }

    private sealed record TransactionInsightRow(
        int UserId,
        int? TargetUserId,
        TransactionType Type,
        decimal Amount,
        decimal Fee,
        DateTime InsertDate,
        long Id = 0,
        int? StockId = null,
        decimal? Shares = null,
        decimal? PriceAtTransaction = null);

    private sealed record UserTransactionRaw(
        long Id,
        int UserId,
        int? TargetUserId,
        int? StockId,
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
        decimal DailyChangePercent,
        decimal PreviousPrice = 0m,
        DateTime InsertedAtUtc = default,
        DateTime LastUpdatedAtUtc = default);

    private sealed record StockHoldingInsightRow(
        int UserId,
        string DiscordId,
        string Username,
        int StockId,
        decimal Shares,
        decimal Price,
        decimal Value,
        decimal TotalInvested,
        string EntityType);

    private sealed record ButtonGameInsightRow(
        int PressId,
        int UserId,
        int? GuildId,
        long Score,
        DateTime InsertDate);

    private sealed record ReminderInsightRow(
        int Id,
        int? GuildId,
        string? GuildName,
        ulong ChannelId,
        int? UserId,
        string Username,
        string Text,
        DateTime InsertDate,
        DateTime DueDate);

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

    private sealed record DashboardUserQuoteTotals(
        int Contributions,
        int ScoreReceived,
        int VotesGiven);

    private sealed record UserRankActivityRow(
        DateTime Date,
        int UserId,
        long Xp);

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
