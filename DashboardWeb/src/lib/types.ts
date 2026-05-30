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

export type DashboardData = {
  overview: DashboardOverviewResponse;
  guilds: DashboardGuildSummary[];
  activity: DashboardActivitySeriesResponse;
  xpLeaderboard: DashboardLeaderboardResponse;
  messageLeaderboard: DashboardLeaderboardResponse;
  quotes: DashboardQuotePageResponse;
  insights: DashboardInsightsResponse;
  usingDemoData: boolean;
  error?: string;
};

export type DashboardFilters = {
  guildId?: number;
  userId?: number;
  channelId?: string;
  days: number;
  scope: "global" | "server" | "user" | "channel";
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
  channels: DashboardChannelActivity[];
  users: DashboardUserActivitySummary[];
  heatmap: DashboardHeatmapCell[];
  quotes: DashboardQuoteInsights;
  economy: DashboardEconomyInsights;
  stocks: DashboardStockMarketInsights;
  buttonGame: DashboardButtonGameInsights;
  operations: DashboardOperationsInsights;
  settings: DashboardGuildSettingsSummary[];
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
  approved: number;
  pending: number;
  removed: number;
  approvalRequests: number;
  pendingApprovalRequests: number;
  averageScore: number;
  statuses: DashboardQuoteStatusSlice[];
  authors: DashboardQuoteAuthorSummary[];
  scoreHistogram: DashboardHistogramBucket[];
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

export type DashboardHistogramBucket = {
  label: string;
  count: number;
};

export type DashboardEconomyInsights = {
  cashBalance: number;
  portfolioValue: number;
  netWorth: number;
  activeWallets: number;
  activeTraders: number;
  transactionVolume: number;
  fees: number;
  dailyFlow: DashboardEconomyFlowPoint[];
  transactionTypes: DashboardCategoryValue[];
  moneyFlows: DashboardMoneyFlow[];
  wealthLeaders: DashboardWealthUser[];
};

export type DashboardEconomyFlowPoint = {
  dateUtc: string;
  inflow: number;
  outflow: number;
  net: number;
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

export type DashboardStockMarketInsights = {
  stocks: number;
  marketValue: number;
  averageDailyChangePercent: number;
  winners: DashboardStockMover[];
  losers: DashboardStockMover[];
  entityTypes: DashboardCategoryValue[];
};

export type DashboardStockMover = {
  stockId: number;
  entityType: string;
  name: string;
  price: number;
  dailyChangePercent: number;
  holdingValue: number;
};

export type DashboardButtonGameInsights = {
  presses: number;
  score: number;
  averageScore: number;
  lastPressAtUtc: string | null;
  daily: DashboardButtonGamePoint[];
  leaders: DashboardButtonGameUser[];
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

export type DashboardOperationsInsights = {
  reminders: DashboardReminderStats;
  moderation: DashboardModerationStats;
  logSeverities: DashboardLogSeveritySlice[];
  logTimeline: DashboardLogPoint[];
  recentLogs: DashboardLogItem[];
};

export type DashboardReminderStats = {
  pending: number;
  overdue: number;
  dueNext24Hours: number;
  upcoming: DashboardReminderItem[];
};

export type DashboardReminderItem = {
  id: number;
  channelId: string;
  text: string;
  user: string;
  dueDateUtc: string;
};

export type DashboardModerationStats = {
  pendingTemporaryBans: number;
  overdueTemporaryBans: number;
  completedLast30Days: number;
  pending: DashboardTemporaryBanItem[];
};

export type DashboardTemporaryBanItem = {
  id: number;
  guildId: string;
  userId: string;
  reason: string | null;
  expiresAtUtc: string;
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
