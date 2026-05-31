namespace Morpheus.Dashboard;

public sealed record DashboardOverviewResponse(
    DateTime GeneratedAtUtc,
    DateTime StartedAtUtc,
    long UptimeSeconds,
    DashboardSystemStats System,
    DashboardActivityStats Activity,
    DashboardQuoteStats Quotes,
    DashboardEconomyStats Economy,
    DashboardLogStats Logs);

public sealed record DashboardGlobalOverviewResponse(
    DateTime GeneratedAtUtc,
    int Days,
    DashboardGlobalTotals Totals,
    DashboardGlobalHighlights Highlights,
    DashboardGlobalVisuals Visuals,
    DashboardGlobalFeeds Feeds);

public sealed record DashboardGlobalTotals(
    int TotalServers,
    int TotalKnownUsers,
    long TotalTrackedMessages,
    long TotalXpGenerated,
    long LatestDayMessages,
    long LatestDayXpGenerated,
    int TotalQuotes,
    int TotalApprovedQuotes,
    int PendingQuotes,
    int PendingQuoteApprovals,
    decimal TotalEconomyBalance,
    decimal TotalEstimatedNetWorth,
    decimal UbiPoolSize,
    decimal SlotsVaultSize,
    long TotalTransactions,
    long TotalButtonPresses,
    int ActiveReminders,
    int RecentWarningsOrErrors);

public sealed record DashboardGlobalHighlights(
    IReadOnlyList<DashboardGlobalServerActivity> MostActiveServersToday,
    IReadOnlyList<DashboardGlobalServerActivity> MostActiveServersThisWeek,
    IReadOnlyList<DashboardGlobalServerActivity> MostActiveServersThisMonth,
    IReadOnlyList<DashboardGlobalServerActivity> MostActiveServersAllTime,
    IReadOnlyList<DashboardGlobalServerActivity> MostActiveServersSelectedWindow,
    IReadOnlyList<DashboardGlobalUserActivity> BiggestXpGainers,
    IReadOnlyList<DashboardGlobalWealthUser> RichestUsersByBalance,
    IReadOnlyList<DashboardGlobalWealthUser> RichestUsersByNetWorth,
    IReadOnlyList<DashboardStockMover> BiggestStockGainers,
    IReadOnlyList<DashboardStockMover> BiggestStockLosers,
    IReadOnlyList<DashboardPopularQuote> MostPopularQuotes,
    IReadOnlyList<DashboardGlobalChannelActivity> MostActiveChannels,
    IReadOnlyList<DashboardGlobalUserActivity> MostActiveUsers,
    IReadOnlyList<DashboardRecentEntity> RecentlyCreatedUsers,
    IReadOnlyList<DashboardRecentEntity> RecentlyCreatedServers,
    IReadOnlyList<DashboardRecentQuote> RecentlyCreatedQuotes,
    IReadOnlyList<DashboardRecentStock> RecentlyCreatedStocks);

public sealed record DashboardGlobalVisuals(
    IReadOnlyList<DashboardActivityDerivedPoint> Activity,
    IReadOnlyList<DashboardStackedServerActivityPoint> StackedServerActivity,
    IReadOnlyList<DashboardCalendarActivityCell> CalendarActivity,
    IReadOnlyList<DashboardHeatmapCell> HourByWeekdayActivity,
    IReadOnlyList<DashboardCategoryValue> TransactionTypes);

public sealed record DashboardGlobalFeeds(
    IReadOnlyList<DashboardEconomyEventItem> RecentEconomyEvents,
    IReadOnlyList<DashboardLogItem> RecentBotHealthEvents);

public sealed record DashboardGlobalServerActivity(
    int Rank,
    int GuildId,
    string DiscordId,
    string Name,
    long Messages,
    long Xp,
    int ActiveUsers,
    DateTime? LastActivityAtUtc);

public sealed record DashboardGlobalUserActivity(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    long Messages,
    long Xp,
    int Level,
    DateTime? LastActivityAtUtc);

public sealed record DashboardGlobalWealthUser(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    decimal Balance,
    decimal StockPortfolioValue,
    decimal NetWorth);

public sealed record DashboardPopularQuote(
    int Rank,
    int Id,
    int GuildId,
    int UserId,
    string Author,
    string Content,
    DateTime InsertedAtUtc,
    int Score);

public sealed record DashboardGlobalChannelActivity(
    int Rank,
    string DiscordId,
    string Name,
    long Messages,
    long Xp,
    int ActiveUsers,
    DateTime? LastActivityAtUtc);

public sealed record DashboardRecentEntity(
    int Id,
    string DiscordId,
    string Name,
    DateTime InsertedAtUtc);

public sealed record DashboardRecentQuote(
    int Id,
    int GuildId,
    int UserId,
    string Author,
    string Content,
    bool Approved,
    bool Removed,
    DateTime InsertedAtUtc);

public sealed record DashboardRecentStock(
    int StockId,
    string EntityType,
    int EntityId,
    string Name,
    decimal Price,
    decimal DailyChangePercent,
    DateTime InsertedAtUtc);

public sealed record DashboardStackedServerActivityPoint(
    DateTime DateUtc,
    int GuildId,
    string GuildName,
    int Messages);

public sealed record DashboardCalendarActivityCell(
    DateTime DateUtc,
    int Messages,
    long Xp,
    int ActiveUsers);

public sealed record DashboardEconomyEventItem(
    long Id,
    string Type,
    decimal Amount,
    decimal Fee,
    int UserId,
    string User,
    int? TargetUserId,
    string? TargetUser,
    int? StockId,
    string? StockName,
    DateTime InsertedAtUtc);

public sealed record DashboardSystemStats(
    int Guilds,
    int Users,
    int Stocks);

public sealed record DashboardActivityStats(
    long TotalMessages,
    long TotalXp,
    int ActiveUsersLast30Days,
    long MessagesLast30Days,
    long XpLast30Days,
    DateTime? LastActivityAtUtc);

public sealed record DashboardQuoteStats(
    int Approved,
    int Pending,
    int Removed,
    int TotalScores);

public sealed record DashboardEconomyStats(
    decimal TotalBalance,
    decimal StockPortfolioValue);

public sealed record DashboardLogStats(
    long Total,
    int Last24Hours,
    DateTime? LastLogAtUtc);

public sealed record DashboardGuildSummary(
    int Id,
    string DiscordId,
    string Name,
    DateTime InsertedAtUtc,
    int TrackedUsers,
    long Messages,
    long Xp,
    int ApprovedQuotes);

public sealed record DashboardActivitySeriesResponse(
    int? GuildId,
    int Days,
    IReadOnlyList<DashboardActivityPoint> Points);

public sealed record DashboardActivityPoint(
    DateTime DateUtc,
    int Messages,
    long Xp,
    int ActiveUsers,
    double AverageMessageLength);

public sealed record DashboardLeaderboardResponse(
    int? GuildId,
    string Metric,
    int? Days,
    int Limit,
    IReadOnlyList<DashboardLeaderboardItem> Items);

public sealed record DashboardLeaderboardItem(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    long Value,
    int? Level,
    DateTime? LastActivityAtUtc);

public sealed record DashboardQuotePageResponse(
    int Page,
    int TotalPages,
    int Total,
    IReadOnlyList<DashboardQuoteItem> Items);

public sealed record DashboardQuoteItem(
    int Id,
    int GuildId,
    int UserId,
    string Author,
    string Content,
    DateTime InsertedAtUtc,
    bool Approved,
    bool Removed,
    int Score);

public sealed record DashboardQuoteDetailsResponse(
    int Id,
    int GuildId,
    string Content,
    DateTime InsertedAtUtc,
    bool Approved,
    bool Removed,
    int TotalScore,
    string Author);

public sealed record DashboardInsightsResponse(
    int? GuildId,
    int? UserId,
    string? ChannelId,
    int Days,
    string Scope,
    string SortDirection,
    int MinActivity,
    DashboardActivityInsights Activity,
    IReadOnlyList<DashboardChannelActivity> Channels,
    IReadOnlyList<DashboardUserActivitySummary> Users,
    IReadOnlyList<DashboardHeatmapCell> Heatmap,
    DashboardQuoteInsights Quotes,
    DashboardEconomyInsights Economy,
    DashboardStockMarketInsights Stocks,
    DashboardButtonGameInsights ButtonGame,
    DashboardOperationsInsights Operations,
    IReadOnlyList<DashboardGuildSettingsSummary> Settings,
    DashboardFilterOptions FilterOptions);

public sealed record DashboardFilterOptions(
    IReadOnlyList<DashboardUserOption> Users,
    IReadOnlyList<DashboardChannelOption> Channels);

public sealed record DashboardUserOption(
    int UserId,
    string DiscordId,
    string Username);

public sealed record DashboardChannelOption(
    string DiscordId,
    string Name);

public sealed record DashboardActivityInsights(
    long Messages,
    long Xp,
    int ActiveUsers,
    int ActiveChannels,
    double AverageMessageLength,
    double MessagesPerActiveUser,
    double XpPerMessage,
    int PeakHourUtc,
    double TrendPercent,
    IReadOnlyList<DashboardActivityDerivedPoint> Points);

public sealed record DashboardActivityDerivedPoint(
    DateTime DateUtc,
    int Messages,
    long Xp,
    int ActiveUsers,
    double RollingMessages,
    long CumulativeMessages,
    long CumulativeXp);

public sealed record DashboardChannelActivity(
    int Rank,
    string DiscordId,
    string Name,
    long Messages,
    long Xp,
    int ActiveUsers,
    double AverageMessageLength,
    DateTime? LastActivityAtUtc);

public sealed record DashboardUserActivitySummary(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    long Messages,
    long Xp,
    int Level,
    int Quotes,
    decimal Balance,
    decimal StockPortfolioValue,
    long ButtonScore,
    double AverageMessageLength,
    DateTime? LastActivityAtUtc);

public sealed record DashboardHeatmapCell(
    int DayOfWeek,
    string DayLabel,
    int HourUtc,
    int Messages,
    long Xp,
    int ActiveUsers);

public sealed record DashboardQuoteInsights(
    int Approved,
    int Pending,
    int Removed,
    int ApprovalRequests,
    int PendingApprovalRequests,
    double AverageScore,
    IReadOnlyList<DashboardQuoteStatusSlice> Statuses,
    IReadOnlyList<DashboardQuoteAuthorSummary> Authors,
    IReadOnlyList<DashboardHistogramBucket> ScoreHistogram,
    IReadOnlyList<DashboardQuoteItem> RecentPending);

public sealed record DashboardQuoteStatusSlice(
    string Status,
    int Count);

public sealed record DashboardQuoteAuthorSummary(
    int UserId,
    string DiscordId,
    string Username,
    int Quotes,
    int Score);

public sealed record DashboardHistogramBucket(
    string Label,
    int Count);

public sealed record DashboardEconomyInsights(
    decimal CashBalance,
    decimal PortfolioValue,
    decimal NetWorth,
    int ActiveWallets,
    int ActiveTraders,
    decimal TransactionVolume,
    decimal Fees,
    IReadOnlyList<DashboardEconomyFlowPoint> DailyFlow,
    IReadOnlyList<DashboardCategoryValue> TransactionTypes,
    IReadOnlyList<DashboardMoneyFlow> MoneyFlows,
    IReadOnlyList<DashboardWealthUser> WealthLeaders);

public sealed record DashboardEconomyFlowPoint(
    DateTime DateUtc,
    decimal Inflow,
    decimal Outflow,
    decimal Net);

public sealed record DashboardCategoryValue(
    string Label,
    decimal Value);

public sealed record DashboardMoneyFlow(
    string Source,
    string Target,
    decimal Value);

public sealed record DashboardWealthUser(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    decimal Balance,
    decimal StockPortfolioValue,
    decimal NetWorth);

public sealed record DashboardStockMarketInsights(
    int Stocks,
    decimal MarketValue,
    double AverageDailyChangePercent,
    IReadOnlyList<DashboardStockMover> Winners,
    IReadOnlyList<DashboardStockMover> Losers,
    IReadOnlyList<DashboardCategoryValue> EntityTypes);

public sealed record DashboardStockMover(
    int StockId,
    string EntityType,
    string Name,
    decimal Price,
    decimal DailyChangePercent,
    decimal HoldingValue);

public sealed record DashboardButtonGameInsights(
    long Presses,
    long Score,
    double AverageScore,
    DateTime? LastPressAtUtc,
    IReadOnlyList<DashboardButtonGamePoint> Daily,
    IReadOnlyList<DashboardButtonGameUser> Leaders);

public sealed record DashboardButtonGamePoint(
    DateTime DateUtc,
    int Presses,
    long Score,
    int ActiveUsers,
    double RollingPresses,
    long CumulativeScore);

public sealed record DashboardButtonGameUser(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    long Presses,
    long Score,
    DateTime? LastPressAtUtc);

public sealed record DashboardOperationsInsights(
    DashboardReminderStats Reminders,
    DashboardModerationStats Moderation,
    IReadOnlyList<DashboardLogSeveritySlice> LogSeverities,
    IReadOnlyList<DashboardLogPoint> LogTimeline,
    IReadOnlyList<DashboardLogItem> RecentLogs);

public sealed record DashboardReminderStats(
    int Pending,
    int Overdue,
    int DueNext24Hours,
    IReadOnlyList<DashboardReminderItem> Upcoming);

public sealed record DashboardReminderItem(
    int Id,
    string ChannelId,
    string Text,
    string User,
    DateTime DueDateUtc);

public sealed record DashboardModerationStats(
    int PendingTemporaryBans,
    int OverdueTemporaryBans,
    int CompletedLast30Days,
    IReadOnlyList<DashboardTemporaryBanItem> Pending);

public sealed record DashboardTemporaryBanItem(
    int Id,
    string GuildId,
    string UserId,
    string? Reason,
    DateTime ExpiresAtUtc);

public sealed record DashboardLogSeveritySlice(
    string Severity,
    int Count);

public sealed record DashboardLogPoint(
    DateTime DateUtc,
    int Total,
    int Warnings,
    int Errors);

public sealed record DashboardLogItem(
    long Id,
    string Severity,
    string Message,
    string Version,
    DateTime InsertedAtUtc);

public sealed record DashboardGuildSettingsSummary(
    int GuildId,
    string GuildName,
    string Prefix,
    bool LevelUpMessages,
    bool LevelUpQuotes,
    bool UseGlobalQuotes,
    bool WelcomeMessages,
    bool UseActivityRoles,
    int QuoteAddRequiredApprovals,
    int QuoteRemoveRequiredApprovals);

internal sealed record DashboardLeaderboardRow(
    int UserId,
    long Value,
    DateTime? LastActivityAtUtc);
