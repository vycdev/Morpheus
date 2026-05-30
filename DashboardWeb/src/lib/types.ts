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
  usingDemoData: boolean;
  error?: string;
};

export type DashboardFilters = {
  guildId?: number;
  days: number;
};
