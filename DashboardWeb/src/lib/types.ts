export type DashboardOverviewResponse = {
  generatedAtUtc: string;
  startedAtUtc: string;
  uptimeSeconds: number;
  system: DashboardSystemStats;
  activity: DashboardActivityStats;
  quotes: DashboardQuoteStats;
  economy: DashboardEconomyStats;
  logs: DashboardLogStats;
};

export type DashboardGlobalOverviewResponse = {
  generatedAtUtc: string;
  days: number;
  totals: DashboardGlobalTotals;
  highlights: DashboardGlobalHighlights;
  visuals: DashboardGlobalVisuals;
  feeds: DashboardGlobalFeeds;
};

export type DashboardGlobalTotals = {
  totalServers: number;
  totalKnownUsers: number;
  totalTrackedMessages: number;
  totalXpGenerated: number;
  latestDayMessages: number;
  latestDayXpGenerated: number;
  totalQuotes: number;
  totalApprovedQuotes: number;
  pendingQuotes: number;
  pendingQuoteApprovals: number;
  totalEconomyBalance: number;
  totalEstimatedNetWorth: number;
  ubiPoolSize: number;
  slotsVaultSize: number;
  totalTransactions: number;
  totalButtonPresses: number;
  activeReminders: number;
  recentWarningsOrErrors: number;
};

export type DashboardGlobalHighlights = {
  mostActiveServersToday: DashboardGlobalServerActivity[];
  mostActiveServersThisWeek: DashboardGlobalServerActivity[];
  mostActiveServersThisMonth: DashboardGlobalServerActivity[];
  mostActiveServersAllTime: DashboardGlobalServerActivity[];
  mostActiveServersSelectedWindow: DashboardGlobalServerActivity[];
  biggestXpGainers: DashboardGlobalUserActivity[];
  richestUsersByBalance: DashboardGlobalWealthUser[];
  richestUsersByNetWorth: DashboardGlobalWealthUser[];
  biggestStockGainers: DashboardStockMover[];
  biggestStockLosers: DashboardStockMover[];
  mostPopularQuotes: DashboardPopularQuote[];
  mostActiveChannels: DashboardGlobalChannelActivity[];
  mostActiveUsers: DashboardGlobalUserActivity[];
  recentlyCreatedUsers: DashboardRecentEntity[];
  recentlyCreatedServers: DashboardRecentEntity[];
  recentlyCreatedQuotes: DashboardRecentQuote[];
  recentlyCreatedStocks: DashboardRecentStock[];
};

export type DashboardGlobalVisuals = {
  activity: DashboardActivityDerivedPoint[];
  stackedServerActivity: DashboardStackedServerActivityPoint[];
  calendarActivity: DashboardCalendarActivityCell[];
  hourByWeekdayActivity: DashboardHeatmapCell[];
  transactionTypes: DashboardCategoryValue[];
};

export type DashboardGlobalFeeds = {
  recentEconomyEvents: DashboardEconomyEventItem[];
  recentBotHealthEvents: DashboardLogItem[];
};

export type DashboardGlobalServerActivity = {
  rank: number;
  guildId: number;
  discordId: string;
  name: string;
  messages: number;
  xp: number;
  activeUsers: number;
  lastActivityAtUtc: string | null;
};

export type DashboardGlobalUserActivity = {
  rank: number;
  userId: number;
  discordId: string;
  username: string;
  messages: number;
  xp: number;
  level: number;
  lastActivityAtUtc: string | null;
};

export type DashboardGlobalWealthUser = {
  rank: number;
  userId: number;
  discordId: string;
  username: string;
  balance: number;
  stockPortfolioValue: number;
  netWorth: number;
};

export type DashboardPopularQuote = {
  rank: number;
  id: number;
  guildId: number;
  userId: number;
  author: string;
  content: string;
  insertedAtUtc: string;
  score: number;
};

export type DashboardGlobalChannelActivity = {
  rank: number;
  discordId: string;
  name: string;
  messages: number;
  xp: number;
  activeUsers: number;
  lastActivityAtUtc: string | null;
};

export type DashboardRecentEntity = {
  id: number;
  discordId: string;
  name: string;
  insertedAtUtc: string;
};

export type DashboardRecentQuote = {
  id: number;
  guildId: number;
  userId: number;
  author: string;
  content: string;
  approved: boolean;
  removed: boolean;
  insertedAtUtc: string;
};

export type DashboardRecentStock = {
  stockId: number;
  entityType: string;
  entityId: number;
  name: string;
  price: number;
  dailyChangePercent: number;
  insertedAtUtc: string;
};

export type DashboardStackedServerActivityPoint = {
  dateUtc: string;
  guildId: number;
  guildName: string;
  messages: number;
};

export type DashboardCalendarActivityCell = {
  dateUtc: string;
  messages: number;
  xp: number;
  activeUsers: number;
};

export type DashboardEconomyEventItem = {
  id: number;
  type: string;
  amount: number;
  fee: number;
  userId: number;
  user: string;
  targetUserId: number | null;
  targetUser: string | null;
  stockId: number | null;
  stockName: string | null;
  insertedAtUtc: string;
};

export type DashboardSystemStats = {
  guilds: number;
  users: number;
  stocks: number;
};

export type DashboardActivityStats = {
  totalMessages: number;
  totalXp: number;
  activeUsersLast30Days: number;
  messagesLast30Days: number;
  xpLast30Days: number;
  lastActivityAtUtc: string | null;
};

export type DashboardQuoteStats = {
  approved: number;
  pending: number;
  removed: number;
  totalScores: number;
};

export type DashboardEconomyStats = {
  totalBalance: number;
  stockPortfolioValue: number;
};

export type DashboardLogStats = {
  total: number;
  last24Hours: number;
  lastLogAtUtc: string | null;
};

export type DashboardGuildSummary = {
  id: number;
  discordId: string;
  name: string;
  insertedAtUtc: string;
  trackedUsers: number;
  messages: number;
  xp: number;
  approvedQuotes: number;
};

export type DashboardActivitySeriesResponse = {
  guildId: number | null;
  days: number;
  points: DashboardActivityPoint[];
};

export type DashboardActivityPoint = {
  dateUtc: string;
  messages: number;
  xp: number;
  activeUsers: number;
  averageMessageLength: number;
};

export type DashboardLeaderboardResponse = {
  guildId: number | null;
  metric: "xp" | "messages";
  days: number | null;
  limit: number;
  items: DashboardLeaderboardItem[];
};

export type DashboardLeaderboardItem = {
  rank: number;
  userId: number;
  discordId: string;
  username: string;
  value: number;
  level: number | null;
  lastActivityAtUtc: string | null;
};

export type DashboardQuotePageResponse = {
  page: number;
  totalPages: number;
  total: number;
  items: DashboardQuoteItem[];
};

export type DashboardQuoteItem = {
  id: number;
  guildId: number;
  userId: number;
  author: string;
  content: string;
  insertedAtUtc: string;
  approved: boolean;
  removed: boolean;
  score: number;
};

export type DashboardQuoteDetailsResponse = {
  id: number;
  guildId: number;
  userId: number;
  guildName: string;
  content: string;
  insertedAtUtc: string;
  approved: boolean;
  removed: boolean;
  totalScore: number;
  author: string;
  voters: DashboardQuoteVoteItem[];
  approvalRequests: DashboardQuoteApprovalRequestItem[];
};

export type DashboardData = {
  globalOverview: DashboardGlobalOverviewResponse;
  overview: DashboardOverviewResponse;
  guilds: DashboardGuildSummary[];
  filterOptions: DashboardFilterOptions;
  drilldown: DashboardDrilldownData | null;
  usingDemoData: boolean;
  error?: string;
  drilldownError?: string;
};

export type DashboardDrilldownData = {
  activity: DashboardActivitySeriesResponse;
  xpLeaderboard: DashboardLeaderboardResponse;
  messageLeaderboard: DashboardLeaderboardResponse;
  quotes: DashboardQuotePageResponse;
  insights: DashboardInsightsResponse;
};

export type DashboardFilters = {
  guildId?: number;
  userId?: number;
  channelId?: string;
  days: number;
  startDate?: string;
  endDate?: string;
  scope: "global" | "server" | "user" | "channel";
  view?: "summary" | "activity" | "servers" | "users" | "quotes" | "economy" | "stocks" | "operations" | "settings";
  sortDirection: "asc" | "desc";
  minActivity: number;
};

export type DashboardInsightsResponse = {
  guildId: number | null;
  userId: number | null;
  channelId: string | null;
  days: number;
  scope: "global" | "server" | "user" | "channel";
  sortDirection: "asc" | "desc";
  minActivity: number;
  activity: DashboardActivityInsights;
  activityAnalytics: DashboardActivityAnalytics;
  channels: DashboardChannelActivity[];
  users: DashboardUserActivitySummary[];
  heatmap: DashboardHeatmapCell[];
  quotes: DashboardQuoteInsights;
  economy: DashboardEconomyInsights;
  stocks: DashboardStockMarketInsights;
  buttonGame: DashboardButtonGameInsights;
  operations: DashboardOperationsInsights;
  settings: DashboardGuildSettingsSummary[];
  server: DashboardServerInsights | null;
  userProfile: DashboardUserProfileInsights | null;
  filterOptions: DashboardFilterOptions;
};

export type DashboardFilterOptions = {
  users: DashboardUserOption[];
  channels: DashboardChannelOption[];
};

export type DashboardUserOption = {
  userId: number;
  discordId: string;
  username: string;
};

export type DashboardChannelOption = {
  discordId: string;
  name: string;
};

export type DashboardActivityInsights = {
  messages: number;
  xp: number;
  activeUsers: number;
  activeChannels: number;
  averageMessageLength: number;
  messagesPerActiveUser: number;
  xpPerMessage: number;
  peakHourUtc: number;
  trendPercent: number;
  points: DashboardActivityDerivedPoint[];
};

export type DashboardActivityDerivedPoint = {
  dateUtc: string;
  messages: number;
  xp: number;
  activeUsers: number;
  rollingMessages: number;
  cumulativeMessages: number;
  cumulativeXp: number;
};

export type DashboardActivityAnalytics = {
  dailyActiveUsers: DashboardTimeBucket[];
  weeklyActiveUsers: DashboardTimeBucket[];
  monthlyActiveUsers: DashboardTimeBucket[];
  bestActivityDays: DashboardTimeBucket[];
  worstActivityDays: DashboardTimeBucket[];
  peakHours: DashboardTimeBucket[];
  peakWeekdays: DashboardTimeBucket[];
  activityStreaks: DashboardUserActivityStreaks;
  comparisonSeries: DashboardActivityComparisonSeries[];
  xpByUser: DashboardActivityDistributionPoint[];
  xpByChannel: DashboardActivityDistributionPoint[];
  xpByServer: DashboardActivityDistributionPoint[];
  messageShareByUser: DashboardActivityDistributionPoint[];
  messageShareByChannel: DashboardActivityDistributionPoint[];
  messageShareByServer: DashboardActivityDistributionPoint[];
  messageLengthHistogram: DashboardHistogramBucket[];
  messageLengthTrend: DashboardUserMessageLengthPoint[];
  messageLengthBoxPlots: DashboardActivityBoxPlotPoint[];
  messageCountVsXp: DashboardActivityScatterPoint[];
  averageLengthVsXp: DashboardActivityScatterPoint[];
  channelHourHeatmap: DashboardChannelHourHeatmapCell[];
  serverDayHeatmap: DashboardServerDayActivityCell[];
  channelDayHeatmap: DashboardChannelHeatmapCell[];
  userContributionPareto: DashboardActivityParetoPoint[];
  leaderboards: DashboardActivityLeaderboardSet[];
  rankMovement: DashboardUserRankMovement[];
};

export type DashboardActivityComparisonSeries = {
  key: string;
  label: string;
  kind: "time-range" | "user" | "server" | "channel" | string;
  points: DashboardActivityDerivedPoint[];
};

export type DashboardActivityDistributionPoint = {
  id: string;
  label: string;
  kind: "user" | "server" | "channel" | string;
  messages: number;
  xp: number;
  sharePercent: number;
};

export type DashboardActivityBoxPlotPoint = {
  label: string;
  kind: string;
  minimum: number;
  q1: number;
  median: number;
  q3: number;
  maximum: number;
  average: number;
  count: number;
};

export type DashboardActivityScatterPoint = {
  id: string;
  label: string;
  kind: "user" | "server" | "channel" | string;
  messages: number;
  xp: number;
  averageMessageLength: number;
  xpPerMessage: number;
};

export type DashboardChannelHourHeatmapCell = {
  channelId: string;
  channelName: string;
  hourUtc: number;
  messages: number;
  xp: number;
  activeUsers: number;
};

export type DashboardServerDayActivityCell = {
  dateUtc: string;
  guildId: number;
  guildName: string;
  messages: number;
  xp: number;
  activeUsers: number;
};

export type DashboardActivityParetoPoint = {
  id: string;
  label: string;
  value: number;
  sharePercent: number;
  cumulativePercent: number;
};

export type DashboardActivityLeaderboardSet = {
  key: string;
  title: string;
  metric: string;
  unit: string;
  items: DashboardActivityLeaderboardItem[];
};

export type DashboardActivityLeaderboardItem = {
  rank: number;
  entityId: string;
  label: string;
  entityType: "user" | "server" | "channel" | string;
  value: number;
  unit: string;
  messages: number;
  xp: number;
  level: number | null;
  averageMessageLength: number;
  xpPerMessage: number;
  lastActivityAtUtc: string | null;
  deltaPercent: number | null;
};

export type DashboardChannelActivity = {
  rank: number;
  discordId: string;
  name: string;
  messages: number;
  xp: number;
  activeUsers: number;
  averageMessageLength: number;
  lastActivityAtUtc: string | null;
};

export type DashboardUserActivitySummary = {
  rank: number;
  userId: number;
  discordId: string;
  username: string;
  messages: number;
  xp: number;
  level: number;
  quotes: number;
  balance: number;
  stockPortfolioValue: number;
  buttonScore: number;
  averageMessageLength: number;
  lastActivityAtUtc: string | null;
};

export type DashboardHeatmapCell = {
  dayOfWeek: number;
  dayLabel: string;
  hourUtc: number;
  messages: number;
  xp: number;
  activeUsers: number;
};

export type DashboardQuoteInsights = {
  total: number;
  approved: number;
  pending: number;
  removed: number;
  approvalRequests: number;
  pendingApprovalRequests: number;
  expiredApprovalRequests: number;
  completedApprovalRequests: number;
  approvalCompletionRate: number;
  averageApprovalTimeHours: number;
  averageScore: number;
  statuses: DashboardQuoteStatusSlice[];
  authors: DashboardQuoteAuthorSummary[];
  creationTimeline: DashboardQuoteTimelinePoint[];
  scoreTrend: DashboardQuoteTimelinePoint[];
  approvalFunnel: DashboardCategoryValue[];
  approvalTimeHistogram: DashboardHistogramBucket[];
  scoreHistogram: DashboardHistogramBucket[];
  approvalActivityCalendar: DashboardCalendarActivityCell[];
  serverSummaries: DashboardQuoteServerSummary[];
  globalVsServerUsage: DashboardCategoryValue[];
  setupSummaries: DashboardQuoteSetupSummary[];
  highestScoringQuotes: DashboardQuoteRankedItem[];
  lowestScoringQuotes: DashboardQuoteRankedItem[];
  mostControversialQuotes: DashboardQuoteRankedItem[];
  mostRemovedQuotes: DashboardQuoteRankedItem[];
  quoteOfTheDayCandidates: DashboardQuoteCandidate[];
  quoteOfTheWeekCandidates: DashboardQuoteCandidate[];
  quoteOfTheMonthCandidates: DashboardQuoteCandidate[];
  topVoters: DashboardQuoteVoteItem[];
  approvalVoters: DashboardQuoteVoteItem[];
  quoteList: DashboardQuoteManagementItem[];
  approvalRequestsList: DashboardQuoteApprovalRequestItem[];
  pendingApprovalQueue: DashboardQuoteApprovalRequestItem[];
  expiredApprovalQueue: DashboardQuoteApprovalRequestItem[];
  removedQuoteList: DashboardQuoteManagementItem[];
  recentPending: DashboardQuoteItem[];
};

export type DashboardQuoteStatusSlice = {
  status: string;
  count: number;
};

export type DashboardQuoteAuthorSummary = {
  userId: number;
  discordId: string;
  username: string;
  quotes: number;
  score: number;
};

export type DashboardQuoteTimelinePoint = {
  dateUtc: string;
  created: number;
  approved: number;
  pending: number;
  removed: number;
  score: number;
  scoreVotes: number;
  approvalVotes: number;
};

export type DashboardQuoteServerSummary = {
  guildId: number;
  discordId: string;
  name: string;
  total: number;
  approved: number;
  pending: number;
  removed: number;
  approvalRequests: number;
  pendingApprovalRequests: number;
  totalScore: number;
  scoreVotes: number;
  usesGlobalQuotes: boolean;
  approvalChannelConfigured: boolean;
  setupHealth: string;
};

export type DashboardQuoteSetupSummary = {
  guildId: number;
  discordId: string;
  name: string;
  usesGlobalQuotes: boolean;
  approvalChannelConfigured: boolean;
  addRequiredApprovals: number;
  removeRequiredApprovals: number;
  health: string;
  issue: string;
};

export type DashboardQuoteRankedItem = {
  rank: number;
  id: number;
  guildId: number;
  guildName: string;
  userId: number;
  author: string;
  content: string;
  insertedAtUtc: string;
  approved: boolean;
  removed: boolean;
  score: number;
  positiveVotes: number;
  negativeVotes: number;
  totalVotes: number;
  controversyScore: number;
};

export type DashboardQuoteCandidate = {
  rank: number;
  period: string;
  id: number;
  guildId: number;
  guildName: string;
  author: string;
  content: string;
  score: number;
  votes: number;
  insertedAtUtc: string;
};

export type DashboardQuoteVoteItem = {
  rank: number;
  userId: string;
  username: string;
  votes: number;
  positiveVotes: number;
  negativeVotes: number;
  score: number;
  lastVotedAtUtc: string | null;
};

export type DashboardQuoteManagementItem = {
  id: number;
  guildId: number;
  guildName: string;
  userId: number;
  discordId: string;
  author: string;
  content: string;
  insertedAtUtc: string;
  approved: boolean;
  removed: boolean;
  score: number;
  scoreVotes: number;
  pendingApprovals: number;
  lastVoteAtUtc: string | null;
};

export type DashboardQuoteApprovalRequestItem = {
  id: number;
  quoteId: number;
  guildId: number;
  guildName: string;
  type: string;
  status: string;
  currentApprovals: number;
  requiredApprovals: number;
  completionPercent: number;
  insertedAtUtc: string;
  completedAtUtc: string | null;
  expiresAtUtc: string;
  expired: boolean;
  quoteContent: string;
  author: string;
};

export type DashboardHistogramBucket = {
  label: string;
  count: number;
};

export type DashboardEconomyInsights = {
  totalMoneySupply: number;
  cashBalance: number;
  portfolioValue: number;
  netWorth: number;
  averageBalance: number;
  medianBalance: number;
  activeWallets: number;
  activeTraders: number;
  transactionVolume: number;
  transactionCount: number;
  fees: number;
  taxesCollected: number;
  ubiPoolSize: number;
  ubiDonations: number;
  wealthTaxImpact: number;
  transfersVolume: number;
  userToUserTransferVolume: number;
  inflows: number;
  outflows: number;
  robberyWins: number;
  robberyLosses: number;
  robberySuccessRate: number;
  slotsWins: number;
  slotsLosses: number;
  slotsVaultSize: number;
  slotsPayoutRatio: number;
  moneySupplyTrend: DashboardEconomySupplyPoint[];
  dailyFlow: DashboardEconomyFlowPoint[];
  ubiPoolTrend: DashboardEconomyFlowPoint[];
  slotsVaultTrend: DashboardEconomyFlowPoint[];
  slotsProfitLoss: DashboardEconomyFlowPoint[];
  transactionVolumeTimeline: DashboardEconomyStackedPoint[];
  transactionTypes: DashboardCategoryValue[];
  moneyFlows: DashboardMoneyFlow[];
  cashLeaders: DashboardWealthUser[];
  wealthLeaders: DashboardWealthUser[];
  balanceDistribution: DashboardHistogramBucket[];
  wealthInequality: DashboardCategoryValue[];
  topDonors: DashboardEconomyActor[];
  biggestRobberies: DashboardEconomyActor[];
  mostRobbedUsers: DashboardEconomyActor[];
  mostSuccessfulRobbers: DashboardEconomyActor[];
  robberyOutcomes: DashboardCategoryValue[];
  biggestSlotsWins: DashboardEconomyActor[];
  biggestSlotsLosses: DashboardEconomyActor[];
  slotsOutcomes: DashboardCategoryValue[];
  economyHeatmap: DashboardEconomyHeatmapCell[];
};

export type DashboardEconomySupplyPoint = {
  dateUtc: string;
  totalMoneySupply: number;
  cashBalance: number;
  ubiPool: number;
  slotsVault: number;
  inflow: number;
  outflow: number;
};

export type DashboardEconomyFlowPoint = {
  dateUtc: string;
  inflow: number;
  outflow: number;
  net: number;
};

export type DashboardEconomyStackedPoint = {
  dateUtc: string;
  stockBuy: number;
  stockSell: number;
  transfer: number;
  donation: number;
  slotsWin: number;
  slotsLoss: number;
  robberyWin: number;
  robberyLoss: number;
  stockTransfer: number;
  fees: number;
  taxes: number;
};

export type DashboardEconomyHeatmapCell = {
  dayOfWeek: number;
  dayLabel: string;
  hourUtc: number;
  transactions: number;
  volume: number;
  activeUsers: number;
};

export type DashboardCategoryValue = {
  label: string;
  value: number;
};

export type DashboardMoneyFlow = {
  source: string;
  target: string;
  value: number;
};

export type DashboardWealthUser = {
  rank: number;
  userId: number;
  discordId: string;
  username: string;
  balance: number;
  stockPortfolioValue: number;
  netWorth: number;
};

export type DashboardEconomyActor = {
  rank: number;
  userId: number;
  discordId: string;
  username: string;
  amount: number;
  count: number;
  secondaryAmount: number;
  label: string;
};

export type DashboardStockMarketInsights = {
  stocks: number;
  userStocks: number;
  serverStocks: number;
  channelStocks: number;
  marketValue: number;
  averagePrice: number;
  averageDailyChangePercent: number;
  buyVolume: number;
  sellVolume: number;
  stockTransferVolume: number;
  winners: DashboardStockMover[];
  losers: DashboardStockMover[];
  entityTypes: DashboardCategoryValue[];
  mostValuableStocks: DashboardStockTableItem[];
  mostHeldStocks: DashboardStockTableItem[];
  mostTradedStocks: DashboardStockTableItem[];
  newestStocks: DashboardStockTableItem[];
  dailyChangeHistogram: DashboardHistogramBucket[];
  priceMovement: DashboardCategoryValue[];
  holdingsByUser: DashboardStockHoldingSummary[];
  holdingsTable: DashboardStockHoldingItem[];
  tradeVolumeTimeline: DashboardStockTradePoint[];
  buyVsSell: DashboardCategoryValue[];
  ownershipConcentration: DashboardCategoryValue[];
  activityToPrice: DashboardStockActivityPricePoint[];
};

export type DashboardStockMover = {
  stockId: number;
  entityType: string;
  name: string;
  price: number;
  dailyChangePercent: number;
  holdingValue: number;
};

export type DashboardStockTableItem = {
  rank: number;
  stockId: number;
  entityType: string;
  entityId: number;
  name: string;
  price: number;
  previousPrice: number;
  dailyChangePercent: number;
  holdingValue: number;
  sharesHeld: number;
  holders: number;
  tradeVolume: number;
  tradeCount: number;
  insertedAtUtc: string;
};

export type DashboardStockHoldingSummary = {
  rank: number;
  userId: number;
  discordId: string;
  username: string;
  portfolioValue: number;
  shares: number;
  holdings: number;
  ownershipPercent: number;
};

export type DashboardStockHoldingItem = {
  rank: number;
  userId: number;
  discordId: string;
  username: string;
  stockId: number;
  stockName: string;
  entityType: string;
  shares: number;
  price: number;
  value: number;
  ownershipPercent: number;
  unrealizedGain: number;
};

export type DashboardStockTradePoint = {
  dateUtc: string;
  buyVolume: number;
  sellVolume: number;
  transferVolume: number;
  trades: number;
};

export type DashboardStockActivityPricePoint = {
  stockId: number;
  name: string;
  entityType: string;
  messages: number;
  xp: number;
  price: number;
  dailyChangePercent: number;
  holdingValue: number;
};

export type DashboardButtonGameInsights = {
  presses: number;
  score: number;
  averageScore: number;
  medianScore: number;
  highestScoreEver: number;
  lastPressAtUtc: string | null;
  daily: DashboardButtonGamePoint[];
  leaders: DashboardButtonGameUser[];
  topGlobalScores: DashboardButtonGameScoreEntry[];
  topServerScores: DashboardButtonGameScoreEntry[];
  topIndividualScores: DashboardButtonGameScoreEntry[];
  topUsersByTotalScore: DashboardButtonGameUser[];
  topUsersByPressCount: DashboardButtonGameUser[];
  scoreDistribution: DashboardHistogramBucket[];
  pressesByServer: DashboardCategoryValue[];
  pressesByHour: DashboardCategoryValue[];
  pressesByWeekday: DashboardCategoryValue[];
  hourByWeekdayHeatmap: DashboardHeatmapCell[];
  calendarHeatmap: DashboardCalendarActivityCell[];
  longestGaps: DashboardButtonGameGap[];
  competitiveServers: DashboardButtonGameServer[];
};

export type DashboardButtonGamePoint = {
  dateUtc: string;
  presses: number;
  score: number;
  activeUsers: number;
  rollingPresses: number;
  cumulativeScore: number;
};

export type DashboardButtonGameUser = {
  rank: number;
  userId: number;
  discordId: string;
  username: string;
  presses: number;
  score: number;
  lastPressAtUtc: string | null;
};

export type DashboardButtonGameScoreEntry = {
  rank: number;
  pressId: number;
  userId: number;
  discordId: string;
  username: string;
  guildId: number | null;
  guildName: string | null;
  score: number;
  insertedAtUtc: string;
};

export type DashboardButtonGameGap = {
  rank: number;
  startedAtUtc: string;
  endedAtUtc: string;
  hours: number;
  previousScore: number;
  nextScore: number;
};

export type DashboardButtonGameServer = {
  rank: number;
  guildId: number;
  guildName: string;
  presses: number;
  score: number;
  activeUsers: number;
  averageScore: number;
  competitiveScore: number;
  lastPressAtUtc: string | null;
};

export type DashboardOperationsInsights = {
  reminders: DashboardReminderStats;
  moderation: DashboardModerationStats;
  logs: DashboardLogInsights;
  logSeverities: DashboardLogSeveritySlice[];
  logTimeline: DashboardLogPoint[];
  recentLogs: DashboardLogItem[];
};

export type DashboardReminderStats = {
  pending: number;
  overdue: number;
  dueNext24Hours: number;
  averageLeadTimeHours: number;
  upcoming: DashboardReminderItem[];
  byServer: DashboardCategoryValue[];
  byUser: DashboardCategoryValue[];
  byChannel: DashboardCategoryValue[];
  creationTrend: DashboardReminderPoint[];
  dueTimeline: DashboardReminderPoint[];
  calendar: DashboardCalendarActivityCell[];
};

export type DashboardReminderItem = {
  id: number;
  channelId: string;
  text: string;
  user: string;
  server: string | null;
  createdAtUtc: string;
  dueDateUtc: string;
  overdue: boolean;
};

export type DashboardReminderPoint = {
  dateUtc: string;
  created: number;
  due: number;
  overdue: number;
  upcoming: number;
};

export type DashboardModerationStats = {
  pendingTemporaryBans: number;
  overdueTemporaryBans: number;
  completedLast30Days: number;
  reactionRoleMessages: number;
  reactionRoleItems: number;
  pending: DashboardTemporaryBanItem[];
  temporaryBanTimeline: DashboardTemporaryBanPoint[];
  banStatus: DashboardCategoryValue[];
  banReasons: DashboardCategoryValue[];
  reactionRoleTypes: DashboardCategoryValue[];
  reactionRoleUsage: DashboardReactionRoleUsage[];
  activityRoleDistribution: DashboardCategoryValue[];
  serverScorecards: DashboardServerConfigurationScorecard[];
  incompleteServerSetup: DashboardServerSetupIssue[];
  riskyConfiguration: DashboardServerSetupIssue[];
};

export type DashboardTemporaryBanItem = {
  id: number;
  guildId: string;
  userId: string;
  reason: string | null;
  insertedAtUtc: string;
  expiresAtUtc: string;
  unbannedAtUtc: string | null;
  status: string;
};

export type DashboardTemporaryBanPoint = {
  dateUtc: string;
  created: number;
  completed: number;
  expiring: number;
  overdue: number;
};

export type DashboardReactionRoleUsage = {
  guildId: number;
  guildName: string;
  messages: number;
  items: number;
  buttonMessages: number;
  emojiMessages: number;
};

export type DashboardServerConfigurationScorecard = {
  guildId: number;
  guildName: string;
  score: number;
  risk: string;
  passedChecks: number;
  failedChecks: number;
  notes: string[];
};

export type DashboardServerSetupIssue = {
  guildId: number;
  guildName: string;
  severity: string;
  label: string;
  detail: string;
};

export type DashboardLogInsights = {
  total: number;
  warnings: number;
  errors: number;
  critical: number;
  latestAtUtc: string | null;
  severityCounts: DashboardLogSeveritySlice[];
  timeline: DashboardLogPoint[];
  recent: DashboardLogItem[];
  logsByVersion: DashboardCategoryValue[];
  commonMessages: DashboardCategoryValue[];
  recentIncidents: DashboardLogItem[];
  healthIndicators: DashboardCategoryValue[];
};

export type DashboardLogSeveritySlice = {
  severity: string;
  count: number;
};

export type DashboardLogPoint = {
  dateUtc: string;
  total: number;
  warnings: number;
  errors: number;
};

export type DashboardLogItem = {
  id: number;
  severity: string;
  message: string;
  version: string;
  insertedAtUtc: string;
};

export type DashboardGuildSettingsSummary = {
  guildId: number;
  guildName: string;
  prefix: string;
  levelUpMessages: boolean;
  levelUpQuotes: boolean;
  useGlobalQuotes: boolean;
  welcomeMessages: boolean;
  useActivityRoles: boolean;
  quoteAddRequiredApprovals: number;
  quoteRemoveRequiredApprovals: number;
};

export type DashboardServerInsights = {
  identity: DashboardServerIdentity;
  totals: DashboardServerTotals;
  configuration: DashboardServerConfiguration;
  health: DashboardServerHealthScorecard;
  configurationChecklist: DashboardServerChecklistItem[];
  topUsersByAverageMessageLength: DashboardRankedUserMetric[];
  fastestRisingUsers: DashboardUserTrend[];
  droppingUsers: DashboardUserTrend[];
  quietestChannels: DashboardChannelActivity[];
  bestActivityDays: DashboardTimeBucket[];
  worstActivityDays: DashboardTimeBucket[];
  peakHours: DashboardTimeBucket[];
  peakWeekdays: DashboardTimeBucket[];
  activityRoleDistribution: DashboardCategoryValue[];
  userRankMovement: DashboardUserRankMovement[];
  channelHeatmap: DashboardChannelHeatmapCell[];
};

export type DashboardServerIdentity = {
  guildId: number;
  discordId: string;
  name: string;
  insertedAtUtc: string;
};

export type DashboardServerTotals = {
  knownUsers: number;
  trackedMessages: number;
  totalXp: number;
  totalQuotes: number;
  approvedQuotes: number;
  pendingQuotes: number;
  pendingQuoteApprovals: number;
  removedQuotes: number;
  activeReminders: number;
  buttonPresses: number;
  economyTransactions: number;
  economyVolume: number;
  stockActivity: number;
  stockMarketValue: number;
  lastActivityAtUtc: string | null;
};

export type DashboardServerConfiguration = {
  prefix: string;
  welcomeChannel: DashboardConfiguredChannel;
  pinsChannel: DashboardConfiguredChannel;
  honeypotChannel: DashboardConfiguredChannel;
  honeypotMessages: boolean;
  levelUpMessages: boolean;
  levelUpMessageChannel: DashboardConfiguredChannel;
  levelUpQuoteMessages: boolean;
  levelUpQuoteChannel: DashboardConfiguredChannel;
  quoteApprovalChannel: DashboardConfiguredChannel;
  quoteAddRequiredApprovals: number;
  quoteRemoveRequiredApprovals: number;
  globalQuotes: boolean;
  activityRoles: boolean;
};

export type DashboardConfiguredChannel = {
  discordId: string;
  name: string;
  configured: boolean;
};

export type DashboardServerHealthScorecard = {
  score: number;
  label: string;
  activityScore: number;
  configurationScore: number;
  operationsScore: number;
  engagementScore: number;
  notes: string[];
};

export type DashboardServerChecklistItem = {
  label: string;
  passed: boolean;
  detail: string;
  severity: "success" | "warning" | "danger" | string;
};

export type DashboardRankedUserMetric = {
  rank: number;
  userId: number;
  discordId: string;
  username: string;
  value: number;
  unit: string;
  lastActivityAtUtc: string | null;
};

export type DashboardUserTrend = {
  rank: number;
  userId: number;
  discordId: string;
  username: string;
  previousMessages: number;
  recentMessages: number;
  delta: number;
  deltaPercent: number;
};

export type DashboardTimeBucket = {
  label: string;
  sort: number;
  messages: number;
  xp: number;
  activeUsers: number;
};

export type DashboardUserRankMovement = {
  userId: number;
  discordId: string;
  username: string;
  previousRank: number | null;
  currentRank: number;
  rankChange: number;
  previousXp: number;
  currentXp: number;
};

export type DashboardChannelHeatmapCell = {
  dateUtc: string;
  channelId: string;
  channelName: string;
  messages: number;
  xp: number;
};

export type DashboardUserProfileInsights = {
  identity: DashboardUserProfileIdentity;
  totals: DashboardUserProfileTotals;
  activity: DashboardActivityInsights;
  serverLevels: DashboardUserServerLevel[];
  bestActivityDays: DashboardTimeBucket[];
  worstActivityDays: DashboardTimeBucket[];
  activityStreaks: DashboardUserActivityStreaks;
  serverContribution: DashboardUserContribution[];
  channelContribution: DashboardUserContribution[];
  globalRank: DashboardUserRankSnapshot;
  serverRanks: DashboardUserRankSnapshot[];
  rankMovement: DashboardUserRankTimelinePoint[];
  quotePerformance: DashboardUserQuotePerformance;
  economyPerformance: DashboardUserEconomyPerformance;
  stockHoldings: DashboardUserStockHolding[];
  buttonGame: DashboardUserButtonGamePerformance;
  reminders: DashboardUserReminderTimelineItem[];
  messageLengthHistogram: DashboardHistogramBucket[];
  messageLengthTrend: DashboardUserMessageLengthPoint[];
  levelProgression: DashboardUserLevelPoint[];
  activityCalendar: DashboardCalendarActivityCell[];
  hourByWeekdayHeatmap: DashboardHeatmapCell[];
};

export type DashboardUserProfileIdentity = {
  userId: number;
  discordId: string;
  username: string;
  insertedAtUtc: string;
  levelUpMessages: boolean;
  levelUpQuotes: boolean;
};

export type DashboardUserProfileTotals = {
  totalXp: number;
  globalLevel: number;
  totalMessages: number;
  averageMessageLength: number;
  messageLengthMovingAverage: number;
  xpPerMessage: number;
  knownServers: number;
  knownChannels: number;
  mostActiveServer: string;
  mostActiveChannel: string;
  quoteContributions: number;
  quoteScoresReceived: number;
  quoteVotesGiven: number;
  economyBalance: number;
  portfolioValue: number;
  estimatedNetWorth: number;
  buttonScore: number;
  lastActivityAtUtc: string | null;
};

export type DashboardUserServerLevel = {
  guildId: number;
  discordId: string;
  name: string;
  level: number;
  totalXp: number;
  messages: number;
  averageMessageLength: number;
  messageLengthMovingAverage: number;
  rank: number;
  rankPopulation: number;
  lastActivityAtUtc: string | null;
};

export type DashboardUserActivityStreaks = {
  currentStreakDays: number;
  currentStreakStartUtc: string | null;
  currentStreakEndUtc: string | null;
  longestStreakDays: number;
  longestStreakStartUtc: string | null;
  longestStreakEndUtc: string | null;
  activeDays: number;
  quietDays: number;
};

export type DashboardUserContribution = {
  id: string;
  label: string;
  messages: number;
  xp: number;
  percent: number;
};

export type DashboardUserRankSnapshot = {
  guildId: number | null;
  scope: string;
  rank: number | null;
  population: number;
  xp: number;
  messages: number;
};

export type DashboardUserRankTimelinePoint = {
  dateUtc: string;
  globalRank: number | null;
  serverRank: number | null;
  userXp: number;
  leadingXp: number;
};

export type DashboardUserQuotePerformance = {
  contributions: number;
  approved: number;
  pending: number;
  removed: number;
  scoreReceived: number;
  votesGiven: number;
  averageScore: number;
  scoreByServer: DashboardCategoryValue[];
  recentQuotes: DashboardQuoteItem[];
};

export type DashboardUserEconomyPerformance = {
  balance: number;
  portfolioValue: number;
  netWorth: number;
  transactionVolume: number;
  feesPaid: number;
  realizedGains: number;
  unrealizedGains: number;
  trades: number;
  donations: DashboardUserDonationStats;
  robbery: DashboardUserOutcomeStats;
  slots: DashboardUserOutcomeStats;
  dailyFlow: DashboardEconomyFlowPoint[];
  transactionTypes: DashboardCategoryValue[];
  recentTransactions: DashboardUserTransactionItem[];
  tradingHistory: DashboardUserTransactionItem[];
};

export type DashboardUserDonationStats = {
  count: number;
  total: number;
  recent: DashboardUserTransactionItem[];
};

export type DashboardUserOutcomeStats = {
  wins: number;
  losses: number;
  won: number;
  lost: number;
  net: number;
  recent: DashboardUserTransactionItem[];
};

export type DashboardUserTransactionItem = {
  id: number;
  type: string;
  amount: number;
  fee: number;
  direction: string;
  counterpartyUserId: number | null;
  counterpartyUsername: string | null;
  stockId: number | null;
  stockName: string | null;
  insertedAtUtc: string;
};

export type DashboardUserStockHolding = {
  stockId: number;
  entityType: string;
  name: string;
  shares: number;
  price: number;
  value: number;
  totalInvested: number;
  unrealizedGain: number;
  dailyChangePercent: number;
};

export type DashboardUserButtonGamePerformance = {
  presses: number;
  score: number;
  averageScore: number;
  bestScore: number;
  lastPressAtUtc: string | null;
  daily: DashboardButtonGamePoint[];
  scoreTimeline: DashboardUserButtonScorePoint[];
};

export type DashboardUserButtonScorePoint = {
  insertedAtUtc: string;
  score: number;
  cumulativeScore: number;
  serverName: string | null;
};

export type DashboardUserReminderTimelineItem = {
  id: number;
  guildName: string;
  channelId: string;
  text: string;
  createdAtUtc: string;
  dueDateUtc: string;
  overdue: boolean;
};

export type DashboardUserMessageLengthPoint = {
  dateUtc: string;
  averageMessageLength: number;
  movingAverage: number;
  messages: number;
};

export type DashboardUserLevelPoint = {
  dateUtc: string;
  totalXp: number;
  level: number;
};
