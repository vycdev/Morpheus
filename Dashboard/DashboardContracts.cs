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
    int UserId,
    string GuildName,
    string Content,
    DateTime InsertedAtUtc,
    bool Approved,
    bool Removed,
    int TotalScore,
    string Author,
    IReadOnlyList<DashboardQuoteVoteItem> Voters,
    IReadOnlyList<DashboardQuoteApprovalRequestItem> ApprovalRequests);

public sealed record DashboardInsightsResponse(
    int? GuildId,
    int? UserId,
    string? ChannelId,
    int Days,
    string Scope,
    string SortDirection,
    int MinActivity,
    DashboardActivityInsights Activity,
    DashboardActivityAnalytics ActivityAnalytics,
    IReadOnlyList<DashboardChannelActivity> Channels,
    IReadOnlyList<DashboardUserActivitySummary> Users,
    IReadOnlyList<DashboardHeatmapCell> Heatmap,
    DashboardQuoteInsights Quotes,
    DashboardEconomyInsights Economy,
    DashboardStockMarketInsights Stocks,
    DashboardButtonGameInsights ButtonGame,
    DashboardOperationsInsights Operations,
    IReadOnlyList<DashboardGuildSettingsSummary> Settings,
    DashboardServerInsights? Server,
    DashboardUserProfileInsights? UserProfile,
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

public sealed record DashboardActivityAnalytics(
    IReadOnlyList<DashboardTimeBucket> DailyActiveUsers,
    IReadOnlyList<DashboardTimeBucket> WeeklyActiveUsers,
    IReadOnlyList<DashboardTimeBucket> MonthlyActiveUsers,
    IReadOnlyList<DashboardTimeBucket> BestActivityDays,
    IReadOnlyList<DashboardTimeBucket> WorstActivityDays,
    IReadOnlyList<DashboardTimeBucket> PeakHours,
    IReadOnlyList<DashboardTimeBucket> PeakWeekdays,
    DashboardUserActivityStreaks ActivityStreaks,
    IReadOnlyList<DashboardActivityComparisonSeries> ComparisonSeries,
    IReadOnlyList<DashboardActivityDistributionPoint> XpByUser,
    IReadOnlyList<DashboardActivityDistributionPoint> XpByChannel,
    IReadOnlyList<DashboardActivityDistributionPoint> XpByServer,
    IReadOnlyList<DashboardActivityDistributionPoint> MessageShareByUser,
    IReadOnlyList<DashboardActivityDistributionPoint> MessageShareByChannel,
    IReadOnlyList<DashboardActivityDistributionPoint> MessageShareByServer,
    IReadOnlyList<DashboardHistogramBucket> MessageLengthHistogram,
    IReadOnlyList<DashboardUserMessageLengthPoint> MessageLengthTrend,
    IReadOnlyList<DashboardActivityBoxPlotPoint> MessageLengthBoxPlots,
    IReadOnlyList<DashboardActivityScatterPoint> MessageCountVsXp,
    IReadOnlyList<DashboardActivityScatterPoint> AverageLengthVsXp,
    IReadOnlyList<DashboardChannelHourHeatmapCell> ChannelHourHeatmap,
    IReadOnlyList<DashboardServerDayActivityCell> ServerDayHeatmap,
    IReadOnlyList<DashboardChannelHeatmapCell> ChannelDayHeatmap,
    IReadOnlyList<DashboardActivityParetoPoint> UserContributionPareto,
    IReadOnlyList<DashboardActivityLeaderboardSet> Leaderboards,
    IReadOnlyList<DashboardUserRankMovement> RankMovement);

public sealed record DashboardActivityComparisonSeries(
    string Key,
    string Label,
    string Kind,
    IReadOnlyList<DashboardActivityDerivedPoint> Points);

public sealed record DashboardActivityDistributionPoint(
    string Id,
    string Label,
    string Kind,
    long Messages,
    long Xp,
    decimal SharePercent);

public sealed record DashboardActivityBoxPlotPoint(
    string Label,
    string Kind,
    double Minimum,
    double Q1,
    double Median,
    double Q3,
    double Maximum,
    double Average,
    int Count);

public sealed record DashboardActivityScatterPoint(
    string Id,
    string Label,
    string Kind,
    long Messages,
    long Xp,
    double AverageMessageLength,
    double XpPerMessage);

public sealed record DashboardChannelHourHeatmapCell(
    string ChannelId,
    string ChannelName,
    int HourUtc,
    int Messages,
    long Xp,
    int ActiveUsers);

public sealed record DashboardServerDayActivityCell(
    DateTime DateUtc,
    int GuildId,
    string GuildName,
    int Messages,
    long Xp,
    int ActiveUsers);

public sealed record DashboardActivityParetoPoint(
    string Id,
    string Label,
    long Value,
    decimal SharePercent,
    decimal CumulativePercent);

public sealed record DashboardActivityLeaderboardSet(
    string Key,
    string Title,
    string Metric,
    string Unit,
    IReadOnlyList<DashboardActivityLeaderboardItem> Items);

public sealed record DashboardActivityLeaderboardItem(
    int Rank,
    string EntityId,
    string Label,
    string EntityType,
    decimal Value,
    string Unit,
    long Messages,
    long Xp,
    int? Level,
    double AverageMessageLength,
    double XpPerMessage,
    DateTime? LastActivityAtUtc,
    double? DeltaPercent);

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
    int Total,
    int Approved,
    int Pending,
    int Removed,
    int ApprovalRequests,
    int PendingApprovalRequests,
    int ExpiredApprovalRequests,
    int CompletedApprovalRequests,
    double ApprovalCompletionRate,
    double AverageApprovalTimeHours,
    double AverageScore,
    IReadOnlyList<DashboardQuoteStatusSlice> Statuses,
    IReadOnlyList<DashboardQuoteAuthorSummary> Authors,
    IReadOnlyList<DashboardQuoteTimelinePoint> CreationTimeline,
    IReadOnlyList<DashboardQuoteTimelinePoint> ScoreTrend,
    IReadOnlyList<DashboardCategoryValue> ApprovalFunnel,
    IReadOnlyList<DashboardHistogramBucket> ApprovalTimeHistogram,
    IReadOnlyList<DashboardHistogramBucket> ScoreHistogram,
    IReadOnlyList<DashboardCalendarActivityCell> ApprovalActivityCalendar,
    IReadOnlyList<DashboardQuoteServerSummary> ServerSummaries,
    IReadOnlyList<DashboardCategoryValue> GlobalVsServerUsage,
    IReadOnlyList<DashboardQuoteSetupSummary> SetupSummaries,
    IReadOnlyList<DashboardQuoteRankedItem> HighestScoringQuotes,
    IReadOnlyList<DashboardQuoteRankedItem> LowestScoringQuotes,
    IReadOnlyList<DashboardQuoteRankedItem> MostControversialQuotes,
    IReadOnlyList<DashboardQuoteRankedItem> MostRemovedQuotes,
    IReadOnlyList<DashboardQuoteCandidate> QuoteOfTheDayCandidates,
    IReadOnlyList<DashboardQuoteCandidate> QuoteOfTheWeekCandidates,
    IReadOnlyList<DashboardQuoteCandidate> QuoteOfTheMonthCandidates,
    IReadOnlyList<DashboardQuoteVoteItem> TopVoters,
    IReadOnlyList<DashboardQuoteVoteItem> ApprovalVoters,
    IReadOnlyList<DashboardQuoteManagementItem> QuoteList,
    IReadOnlyList<DashboardQuoteApprovalRequestItem> ApprovalRequestsList,
    IReadOnlyList<DashboardQuoteApprovalRequestItem> PendingApprovalQueue,
    IReadOnlyList<DashboardQuoteApprovalRequestItem> ExpiredApprovalQueue,
    IReadOnlyList<DashboardQuoteManagementItem> RemovedQuoteList,
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

public sealed record DashboardQuoteTimelinePoint(
    DateTime DateUtc,
    int Created,
    int Approved,
    int Pending,
    int Removed,
    int Score,
    int ScoreVotes,
    int ApprovalVotes);

public sealed record DashboardQuoteServerSummary(
    int GuildId,
    string DiscordId,
    string Name,
    int Total,
    int Approved,
    int Pending,
    int Removed,
    int ApprovalRequests,
    int PendingApprovalRequests,
    int TotalScore,
    int ScoreVotes,
    bool UsesGlobalQuotes,
    bool ApprovalChannelConfigured,
    string SetupHealth);

public sealed record DashboardQuoteSetupSummary(
    int GuildId,
    string DiscordId,
    string Name,
    bool UsesGlobalQuotes,
    bool ApprovalChannelConfigured,
    int AddRequiredApprovals,
    int RemoveRequiredApprovals,
    string Health,
    string Issue);

public sealed record DashboardQuoteRankedItem(
    int Rank,
    int Id,
    int GuildId,
    string GuildName,
    int UserId,
    string Author,
    string Content,
    DateTime InsertedAtUtc,
    bool Approved,
    bool Removed,
    int Score,
    int PositiveVotes,
    int NegativeVotes,
    int TotalVotes,
    int ControversyScore);

public sealed record DashboardQuoteCandidate(
    int Rank,
    string Period,
    int Id,
    int GuildId,
    string GuildName,
    string Author,
    string Content,
    int Score,
    int Votes,
    DateTime InsertedAtUtc);

public sealed record DashboardQuoteVoteItem(
    int Rank,
    string UserId,
    string Username,
    int Votes,
    int PositiveVotes,
    int NegativeVotes,
    int Score,
    DateTime? LastVotedAtUtc);

public sealed record DashboardQuoteManagementItem(
    int Id,
    int GuildId,
    string GuildName,
    int UserId,
    string DiscordId,
    string Author,
    string Content,
    DateTime InsertedAtUtc,
    bool Approved,
    bool Removed,
    int Score,
    int ScoreVotes,
    int PendingApprovals,
    DateTime? LastVoteAtUtc);

public sealed record DashboardQuoteApprovalRequestItem(
    int Id,
    int QuoteId,
    int GuildId,
    string GuildName,
    string Type,
    string Status,
    int CurrentApprovals,
    int RequiredApprovals,
    double CompletionPercent,
    DateTime InsertedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime ExpiresAtUtc,
    bool Expired,
    string QuoteContent,
    string Author);

public sealed record DashboardHistogramBucket(
    string Label,
    int Count);

public sealed record DashboardEconomyInsights(
    decimal TotalMoneySupply,
    decimal CashBalance,
    decimal PortfolioValue,
    decimal NetWorth,
    decimal AverageBalance,
    decimal MedianBalance,
    int ActiveWallets,
    int ActiveTraders,
    decimal TransactionVolume,
    int TransactionCount,
    decimal Fees,
    decimal TaxesCollected,
    decimal UbiPoolSize,
    decimal UbiDonations,
    decimal WealthTaxImpact,
    decimal TransfersVolume,
    decimal UserToUserTransferVolume,
    decimal Inflows,
    decimal Outflows,
    int RobberyWins,
    int RobberyLosses,
    double RobberySuccessRate,
    int SlotsWins,
    int SlotsLosses,
    decimal SlotsVaultSize,
    double SlotsPayoutRatio,
    IReadOnlyList<DashboardEconomySupplyPoint> MoneySupplyTrend,
    IReadOnlyList<DashboardEconomyFlowPoint> DailyFlow,
    IReadOnlyList<DashboardEconomyFlowPoint> UbiPoolTrend,
    IReadOnlyList<DashboardEconomyFlowPoint> SlotsVaultTrend,
    IReadOnlyList<DashboardEconomyFlowPoint> SlotsProfitLoss,
    IReadOnlyList<DashboardEconomyStackedPoint> TransactionVolumeTimeline,
    IReadOnlyList<DashboardCategoryValue> TransactionTypes,
    IReadOnlyList<DashboardMoneyFlow> MoneyFlows,
    IReadOnlyList<DashboardWealthUser> CashLeaders,
    IReadOnlyList<DashboardWealthUser> WealthLeaders,
    IReadOnlyList<DashboardHistogramBucket> BalanceDistribution,
    IReadOnlyList<DashboardCategoryValue> WealthInequality,
    IReadOnlyList<DashboardEconomyActor> TopDonors,
    IReadOnlyList<DashboardEconomyActor> BiggestRobberies,
    IReadOnlyList<DashboardEconomyActor> MostRobbedUsers,
    IReadOnlyList<DashboardEconomyActor> MostSuccessfulRobbers,
    IReadOnlyList<DashboardCategoryValue> RobberyOutcomes,
    IReadOnlyList<DashboardEconomyActor> BiggestSlotsWins,
    IReadOnlyList<DashboardEconomyActor> BiggestSlotsLosses,
    IReadOnlyList<DashboardCategoryValue> SlotsOutcomes,
    IReadOnlyList<DashboardEconomyHeatmapCell> EconomyHeatmap);

public sealed record DashboardEconomySupplyPoint(
    DateTime DateUtc,
    decimal TotalMoneySupply,
    decimal CashBalance,
    decimal UbiPool,
    decimal SlotsVault,
    decimal Inflow,
    decimal Outflow);

public sealed record DashboardEconomyFlowPoint(
    DateTime DateUtc,
    decimal Inflow,
    decimal Outflow,
    decimal Net);

public sealed record DashboardEconomyStackedPoint(
    DateTime DateUtc,
    decimal StockBuy,
    decimal StockSell,
    decimal Transfer,
    decimal Donation,
    decimal SlotsWin,
    decimal SlotsLoss,
    decimal RobberyWin,
    decimal RobberyLoss,
    decimal StockTransfer,
    decimal Fees,
    decimal Taxes);

public sealed record DashboardEconomyHeatmapCell(
    int DayOfWeek,
    string DayLabel,
    int HourUtc,
    int Transactions,
    decimal Volume,
    int ActiveUsers);

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

public sealed record DashboardEconomyActor(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    decimal Amount,
    int Count,
    decimal SecondaryAmount,
    string Label);

public sealed record DashboardStockMarketInsights(
    int Stocks,
    int UserStocks,
    int ServerStocks,
    int ChannelStocks,
    decimal MarketValue,
    decimal AveragePrice,
    double AverageDailyChangePercent,
    decimal BuyVolume,
    decimal SellVolume,
    decimal StockTransferVolume,
    IReadOnlyList<DashboardStockMover> Winners,
    IReadOnlyList<DashboardStockMover> Losers,
    IReadOnlyList<DashboardCategoryValue> EntityTypes,
    IReadOnlyList<DashboardStockTableItem> MostValuableStocks,
    IReadOnlyList<DashboardStockTableItem> MostHeldStocks,
    IReadOnlyList<DashboardStockTableItem> MostTradedStocks,
    IReadOnlyList<DashboardStockTableItem> NewestStocks,
    IReadOnlyList<DashboardHistogramBucket> DailyChangeHistogram,
    IReadOnlyList<DashboardCategoryValue> PriceMovement,
    IReadOnlyList<DashboardStockHoldingSummary> HoldingsByUser,
    IReadOnlyList<DashboardStockHoldingItem> HoldingsTable,
    IReadOnlyList<DashboardStockTradePoint> TradeVolumeTimeline,
    IReadOnlyList<DashboardCategoryValue> BuyVsSell,
    IReadOnlyList<DashboardCategoryValue> OwnershipConcentration,
    IReadOnlyList<DashboardStockActivityPricePoint> ActivityToPrice);

public sealed record DashboardStockMover(
    int StockId,
    string EntityType,
    string Name,
    decimal Price,
    decimal DailyChangePercent,
    decimal HoldingValue);

public sealed record DashboardStockTableItem(
    int Rank,
    int StockId,
    string EntityType,
    int EntityId,
    string Name,
    decimal Price,
    decimal PreviousPrice,
    decimal DailyChangePercent,
    decimal HoldingValue,
    decimal SharesHeld,
    int Holders,
    decimal TradeVolume,
    int TradeCount,
    DateTime InsertedAtUtc);

public sealed record DashboardStockHoldingSummary(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    decimal PortfolioValue,
    decimal Shares,
    int Holdings,
    decimal OwnershipPercent);

public sealed record DashboardStockHoldingItem(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    int StockId,
    string StockName,
    string EntityType,
    decimal Shares,
    decimal Price,
    decimal Value,
    decimal OwnershipPercent,
    decimal UnrealizedGain);

public sealed record DashboardStockTradePoint(
    DateTime DateUtc,
    decimal BuyVolume,
    decimal SellVolume,
    decimal TransferVolume,
    int Trades);

public sealed record DashboardStockActivityPricePoint(
    int StockId,
    string Name,
    string EntityType,
    long Messages,
    long Xp,
    decimal Price,
    decimal DailyChangePercent,
    decimal HoldingValue);

public sealed record DashboardButtonGameInsights(
    long Presses,
    long Score,
    double AverageScore,
    double MedianScore,
    long HighestScoreEver,
    DateTime? LastPressAtUtc,
    IReadOnlyList<DashboardButtonGamePoint> Daily,
    IReadOnlyList<DashboardButtonGameUser> Leaders,
    IReadOnlyList<DashboardButtonGameScoreEntry> TopGlobalScores,
    IReadOnlyList<DashboardButtonGameScoreEntry> TopServerScores,
    IReadOnlyList<DashboardButtonGameScoreEntry> TopIndividualScores,
    IReadOnlyList<DashboardButtonGameUser> TopUsersByTotalScore,
    IReadOnlyList<DashboardButtonGameUser> TopUsersByPressCount,
    IReadOnlyList<DashboardHistogramBucket> ScoreDistribution,
    IReadOnlyList<DashboardCategoryValue> PressesByServer,
    IReadOnlyList<DashboardCategoryValue> PressesByHour,
    IReadOnlyList<DashboardCategoryValue> PressesByWeekday,
    IReadOnlyList<DashboardHeatmapCell> HourByWeekdayHeatmap,
    IReadOnlyList<DashboardCalendarActivityCell> CalendarHeatmap,
    IReadOnlyList<DashboardButtonGameGap> LongestGaps,
    IReadOnlyList<DashboardButtonGameServer> CompetitiveServers);

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

public sealed record DashboardButtonGameScoreEntry(
    int Rank,
    int PressId,
    int UserId,
    string DiscordId,
    string Username,
    int? GuildId,
    string? GuildName,
    long Score,
    DateTime InsertedAtUtc);

public sealed record DashboardButtonGameGap(
    int Rank,
    DateTime StartedAtUtc,
    DateTime EndedAtUtc,
    double Hours,
    long PreviousScore,
    long NextScore);

public sealed record DashboardButtonGameServer(
    int Rank,
    int GuildId,
    string GuildName,
    long Presses,
    long Score,
    int ActiveUsers,
    double AverageScore,
    double CompetitiveScore,
    DateTime? LastPressAtUtc);

public sealed record DashboardOperationsInsights(
    DashboardReminderStats Reminders,
    DashboardModerationStats Moderation,
    DashboardLogInsights Logs,
    IReadOnlyList<DashboardLogSeveritySlice> LogSeverities,
    IReadOnlyList<DashboardLogPoint> LogTimeline,
    IReadOnlyList<DashboardLogItem> RecentLogs);

public sealed record DashboardReminderStats(
    int Pending,
    int Overdue,
    int DueNext24Hours,
    double AverageLeadTimeHours,
    IReadOnlyList<DashboardReminderItem> Upcoming,
    IReadOnlyList<DashboardCategoryValue> ByServer,
    IReadOnlyList<DashboardCategoryValue> ByUser,
    IReadOnlyList<DashboardCategoryValue> ByChannel,
    IReadOnlyList<DashboardReminderPoint> CreationTrend,
    IReadOnlyList<DashboardReminderPoint> DueTimeline,
    IReadOnlyList<DashboardCalendarActivityCell> Calendar);

public sealed record DashboardReminderItem(
    int Id,
    string ChannelId,
    string Text,
    string User,
    string? Server,
    DateTime CreatedAtUtc,
    DateTime DueDateUtc,
    bool Overdue);

public sealed record DashboardReminderPoint(
    DateTime DateUtc,
    int Created,
    int Due,
    int Overdue,
    int Upcoming);

public sealed record DashboardModerationStats(
    int PendingTemporaryBans,
    int OverdueTemporaryBans,
    int CompletedLast30Days,
    int ReactionRoleMessages,
    int ReactionRoleItems,
    IReadOnlyList<DashboardTemporaryBanItem> Pending,
    IReadOnlyList<DashboardTemporaryBanPoint> TemporaryBanTimeline,
    IReadOnlyList<DashboardCategoryValue> BanStatus,
    IReadOnlyList<DashboardCategoryValue> BanReasons,
    IReadOnlyList<DashboardCategoryValue> ReactionRoleTypes,
    IReadOnlyList<DashboardReactionRoleUsage> ReactionRoleUsage,
    IReadOnlyList<DashboardCategoryValue> ActivityRoleDistribution,
    IReadOnlyList<DashboardServerConfigurationScorecard> ServerScorecards,
    IReadOnlyList<DashboardServerSetupIssue> IncompleteServerSetup,
    IReadOnlyList<DashboardServerSetupIssue> RiskyConfiguration);

public sealed record DashboardTemporaryBanItem(
    int Id,
    string GuildId,
    string UserId,
    string? Reason,
    DateTime InsertedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? UnbannedAtUtc,
    string Status);

public sealed record DashboardTemporaryBanPoint(
    DateTime DateUtc,
    int Created,
    int Completed,
    int Expiring,
    int Overdue);

public sealed record DashboardReactionRoleUsage(
    int GuildId,
    string GuildName,
    int Messages,
    int Items,
    int ButtonMessages,
    int EmojiMessages);

public sealed record DashboardServerConfigurationScorecard(
    int GuildId,
    string GuildName,
    int Score,
    string Risk,
    int PassedChecks,
    int FailedChecks,
    IReadOnlyList<string> Notes);

public sealed record DashboardServerSetupIssue(
    int GuildId,
    string GuildName,
    string Severity,
    string Label,
    string Detail);

public sealed record DashboardLogInsights(
    int Total,
    int Warnings,
    int Errors,
    int Critical,
    DateTime? LatestAtUtc,
    IReadOnlyList<DashboardLogSeveritySlice> SeverityCounts,
    IReadOnlyList<DashboardLogPoint> Timeline,
    IReadOnlyList<DashboardLogItem> Recent,
    IReadOnlyList<DashboardCategoryValue> LogsByVersion,
    IReadOnlyList<DashboardCategoryValue> CommonMessages,
    IReadOnlyList<DashboardLogItem> RecentIncidents,
    IReadOnlyList<DashboardCategoryValue> HealthIndicators);

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

public sealed record DashboardServerInsights(
    DashboardServerIdentity Identity,
    DashboardServerTotals Totals,
    DashboardServerConfiguration Configuration,
    DashboardServerHealthScorecard Health,
    IReadOnlyList<DashboardServerChecklistItem> ConfigurationChecklist,
    IReadOnlyList<DashboardRankedUserMetric> TopUsersByAverageMessageLength,
    IReadOnlyList<DashboardUserTrend> FastestRisingUsers,
    IReadOnlyList<DashboardUserTrend> DroppingUsers,
    IReadOnlyList<DashboardChannelActivity> QuietestChannels,
    IReadOnlyList<DashboardTimeBucket> BestActivityDays,
    IReadOnlyList<DashboardTimeBucket> WorstActivityDays,
    IReadOnlyList<DashboardTimeBucket> PeakHours,
    IReadOnlyList<DashboardTimeBucket> PeakWeekdays,
    IReadOnlyList<DashboardCategoryValue> ActivityRoleDistribution,
    IReadOnlyList<DashboardUserRankMovement> UserRankMovement,
    IReadOnlyList<DashboardChannelHeatmapCell> ChannelHeatmap);

public sealed record DashboardServerIdentity(
    int GuildId,
    string DiscordId,
    string Name,
    DateTime InsertedAtUtc);

public sealed record DashboardServerTotals(
    int KnownUsers,
    long TrackedMessages,
    long TotalXp,
    int TotalQuotes,
    int ApprovedQuotes,
    int PendingQuotes,
    int PendingQuoteApprovals,
    int RemovedQuotes,
    int ActiveReminders,
    long ButtonPresses,
    long EconomyTransactions,
    decimal EconomyVolume,
    int StockActivity,
    decimal StockMarketValue,
    DateTime? LastActivityAtUtc);

public sealed record DashboardServerConfiguration(
    string Prefix,
    DashboardConfiguredChannel WelcomeChannel,
    DashboardConfiguredChannel PinsChannel,
    DashboardConfiguredChannel HoneypotChannel,
    bool HoneypotMessages,
    bool LevelUpMessages,
    DashboardConfiguredChannel LevelUpMessageChannel,
    bool LevelUpQuoteMessages,
    DashboardConfiguredChannel LevelUpQuoteChannel,
    DashboardConfiguredChannel QuoteApprovalChannel,
    int QuoteAddRequiredApprovals,
    int QuoteRemoveRequiredApprovals,
    bool GlobalQuotes,
    bool ActivityRoles);

public sealed record DashboardConfiguredChannel(
    string DiscordId,
    string Name,
    bool Configured);

public sealed record DashboardServerHealthScorecard(
    int Score,
    string Label,
    int ActivityScore,
    int ConfigurationScore,
    int OperationsScore,
    int EngagementScore,
    IReadOnlyList<string> Notes);

public sealed record DashboardServerChecklistItem(
    string Label,
    bool Passed,
    string Detail,
    string Severity);

public sealed record DashboardRankedUserMetric(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    double Value,
    string Unit,
    DateTime? LastActivityAtUtc);

public sealed record DashboardUserTrend(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    long PreviousMessages,
    long RecentMessages,
    long Delta,
    double DeltaPercent);

public sealed record DashboardTimeBucket(
    string Label,
    int Sort,
    int Messages,
    long Xp,
    int ActiveUsers);

public sealed record DashboardUserRankMovement(
    int UserId,
    string DiscordId,
    string Username,
    int? PreviousRank,
    int CurrentRank,
    int RankChange,
    long PreviousXp,
    long CurrentXp);

public sealed record DashboardChannelHeatmapCell(
    DateTime DateUtc,
    string ChannelId,
    string ChannelName,
    int Messages,
    long Xp);

public sealed record DashboardUserProfileInsights(
    DashboardUserProfileIdentity Identity,
    DashboardUserProfileTotals Totals,
    DashboardActivityInsights Activity,
    IReadOnlyList<DashboardUserServerLevel> ServerLevels,
    IReadOnlyList<DashboardTimeBucket> BestActivityDays,
    IReadOnlyList<DashboardTimeBucket> WorstActivityDays,
    DashboardUserActivityStreaks ActivityStreaks,
    IReadOnlyList<DashboardUserContribution> ServerContribution,
    IReadOnlyList<DashboardUserContribution> ChannelContribution,
    DashboardUserRankSnapshot GlobalRank,
    IReadOnlyList<DashboardUserRankSnapshot> ServerRanks,
    IReadOnlyList<DashboardUserRankTimelinePoint> RankMovement,
    DashboardUserQuotePerformance QuotePerformance,
    DashboardUserEconomyPerformance EconomyPerformance,
    IReadOnlyList<DashboardUserStockHolding> StockHoldings,
    DashboardUserButtonGamePerformance ButtonGame,
    IReadOnlyList<DashboardUserReminderTimelineItem> Reminders,
    IReadOnlyList<DashboardHistogramBucket> MessageLengthHistogram,
    IReadOnlyList<DashboardUserMessageLengthPoint> MessageLengthTrend,
    IReadOnlyList<DashboardUserLevelPoint> LevelProgression,
    IReadOnlyList<DashboardCalendarActivityCell> ActivityCalendar,
    IReadOnlyList<DashboardHeatmapCell> HourByWeekdayHeatmap);

public sealed record DashboardUserProfileIdentity(
    int UserId,
    string DiscordId,
    string Username,
    DateTime InsertedAtUtc,
    bool LevelUpMessages,
    bool LevelUpQuotes);

public sealed record DashboardUserProfileTotals(
    long TotalXp,
    int GlobalLevel,
    long TotalMessages,
    double AverageMessageLength,
    double MessageLengthMovingAverage,
    double XpPerMessage,
    int KnownServers,
    int KnownChannels,
    string MostActiveServer,
    string MostActiveChannel,
    int QuoteContributions,
    int QuoteScoresReceived,
    int QuoteVotesGiven,
    decimal EconomyBalance,
    decimal PortfolioValue,
    decimal EstimatedNetWorth,
    long ButtonScore,
    DateTime? LastActivityAtUtc);

public sealed record DashboardUserServerLevel(
    int GuildId,
    string DiscordId,
    string Name,
    int Level,
    long TotalXp,
    long Messages,
    double AverageMessageLength,
    double MessageLengthMovingAverage,
    int Rank,
    int RankPopulation,
    DateTime? LastActivityAtUtc);

public sealed record DashboardUserActivityStreaks(
    int CurrentStreakDays,
    DateTime? CurrentStreakStartUtc,
    DateTime? CurrentStreakEndUtc,
    int LongestStreakDays,
    DateTime? LongestStreakStartUtc,
    DateTime? LongestStreakEndUtc,
    int ActiveDays,
    int QuietDays);

public sealed record DashboardUserContribution(
    string Id,
    string Label,
    long Messages,
    long Xp,
    decimal Percent);

public sealed record DashboardUserRankSnapshot(
    int? GuildId,
    string Scope,
    int? Rank,
    int Population,
    long Xp,
    long Messages);

public sealed record DashboardUserRankTimelinePoint(
    DateTime DateUtc,
    int? GlobalRank,
    int? ServerRank,
    long UserXp,
    long LeadingXp);

public sealed record DashboardUserQuotePerformance(
    int Contributions,
    int Approved,
    int Pending,
    int Removed,
    int ScoreReceived,
    int VotesGiven,
    double AverageScore,
    IReadOnlyList<DashboardCategoryValue> ScoreByServer,
    IReadOnlyList<DashboardQuoteItem> RecentQuotes);

public sealed record DashboardUserEconomyPerformance(
    decimal Balance,
    decimal PortfolioValue,
    decimal NetWorth,
    decimal TransactionVolume,
    decimal FeesPaid,
    decimal RealizedGains,
    decimal UnrealizedGains,
    int Trades,
    DashboardUserDonationStats Donations,
    DashboardUserOutcomeStats Robbery,
    DashboardUserOutcomeStats Slots,
    IReadOnlyList<DashboardEconomyFlowPoint> DailyFlow,
    IReadOnlyList<DashboardCategoryValue> TransactionTypes,
    IReadOnlyList<DashboardUserTransactionItem> RecentTransactions,
    IReadOnlyList<DashboardUserTransactionItem> TradingHistory);

public sealed record DashboardUserDonationStats(
    int Count,
    decimal Total,
    IReadOnlyList<DashboardUserTransactionItem> Recent);

public sealed record DashboardUserOutcomeStats(
    int Wins,
    int Losses,
    decimal Won,
    decimal Lost,
    decimal Net,
    IReadOnlyList<DashboardUserTransactionItem> Recent);

public sealed record DashboardUserTransactionItem(
    long Id,
    string Type,
    decimal Amount,
    decimal Fee,
    string Direction,
    int? CounterpartyUserId,
    string? CounterpartyUsername,
    int? StockId,
    string? StockName,
    DateTime InsertedAtUtc);

public sealed record DashboardUserStockHolding(
    int StockId,
    string EntityType,
    string Name,
    decimal Shares,
    decimal Price,
    decimal Value,
    decimal TotalInvested,
    decimal UnrealizedGain,
    decimal DailyChangePercent);

public sealed record DashboardUserButtonGamePerformance(
    long Presses,
    long Score,
    double AverageScore,
    long BestScore,
    DateTime? LastPressAtUtc,
    IReadOnlyList<DashboardButtonGamePoint> Daily,
    IReadOnlyList<DashboardUserButtonScorePoint> ScoreTimeline);

public sealed record DashboardUserButtonScorePoint(
    DateTime InsertedAtUtc,
    long Score,
    long CumulativeScore,
    string? ServerName);

public sealed record DashboardUserReminderTimelineItem(
    int Id,
    string GuildName,
    string ChannelId,
    string Text,
    DateTime CreatedAtUtc,
    DateTime DueDateUtc,
    bool Overdue);

public sealed record DashboardUserMessageLengthPoint(
    DateTime DateUtc,
    double AverageMessageLength,
    double MovingAverage,
    int Messages);

public sealed record DashboardUserLevelPoint(
    DateTime DateUtc,
    long TotalXp,
    int Level);

internal sealed record DashboardLeaderboardRow(
    int UserId,
    long Value,
    DateTime? LastActivityAtUtc);
