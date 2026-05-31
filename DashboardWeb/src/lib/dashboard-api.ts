import { createDemoDashboardData } from "@/lib/demo-data";
import type {
  DashboardActivitySeriesResponse,
  DashboardData,
  DashboardDrilldownData,
  DashboardFilters,
  DashboardFilterOptions,
  DashboardGlobalOverviewResponse,
  DashboardGuildSummary,
  DashboardInsightsResponse,
  DashboardLeaderboardResponse,
  DashboardOverviewResponse,
  DashboardQuoteApprovalRequestItem,
  DashboardQuoteDetailsResponse,
  DashboardQuotePageResponse,
} from "@/lib/types";
import { clamp } from "@/lib/utils";

const defaultApiUrl = "http://127.0.0.1:5267";
const dashboardRequestTimeoutMs = 7000;
const dashboardResponseCacheTtlMs = 10000;
const dashboardResponseCacheMaxEntries = 128;
const dashboardResponseCache = new Map<string, { expiresAt: number; value: unknown }>();

export async function getDashboardData(filters: DashboardFilters): Promise<DashboardData> {
  const safeFilters = {
    guildId: filters.guildId,
    userId: filters.userId,
    channelId: filters.channelId,
    days: clamp(filters.days, 1, 3650),
    startDate: filters.startDate,
    endDate: filters.endDate,
    scope: filters.scope,
    view: filters.view ?? "summary",
    sortDirection: filters.sortDirection,
    minActivity: clamp(filters.minActivity, 0, 100000),
  };

  try {
    const hasScopedSelection =
      safeFilters.scope === "server" ? Boolean(safeFilters.guildId) :
      safeFilters.scope === "user" ? Boolean(safeFilters.userId) :
      safeFilters.scope === "channel" ? Boolean(safeFilters.channelId) :
      false;
    const shouldFetchDrilldown =
      hasScopedSelection ||
      (safeFilters.scope === "global" &&
        (safeFilters.view === "activity" ||
          safeFilters.view === "quotes" ||
          safeFilters.view === "economy" ||
          safeFilters.view === "stocks" ||
          safeFilters.view === "operations" ||
          safeFilters.view === "settings"));
    const globalOverviewView = safeFilters.scope === "global" && safeFilters.view !== "summary"
      ? "all"
      : "summary";
    const shouldFetchOverview =
      shouldFetchDrilldown && (safeFilters.view === "summary" || safeFilters.view === "activity");
    const shouldFetchFullGuilds = shouldFetchDrilldown && safeFilters.view === "settings";
    const globalOverviewRequest = dashboardRequest<DashboardGlobalOverviewResponse>("/global-overview", {
      days: safeFilters.days,
      startDate: safeFilters.startDate,
      endDate: safeFilters.endDate,
      view: globalOverviewView,
    });
    const [globalOverview, overview, guilds] = await Promise.all([
      safeFilters.scope === "global"
        ? globalOverviewRequest
        : globalOverviewRequest.catch(() => createEmptyGlobalOverview(safeFilters.days)),
      shouldFetchOverview
        ? dashboardRequest<DashboardOverviewResponse>("/overview")
        : Promise.resolve<DashboardOverviewResponse | null>(null),
      getGuildSummaries(shouldFetchFullGuilds),
    ]);
    let drilldown: DashboardDrilldownData | null = null;
    let drilldownError: string | undefined;
    let filterOptions: DashboardFilterOptions = createEmptyFilterOptions();

    if (shouldFetchDrilldown) {
      try {
        drilldown = await getDashboardDrilldownData(safeFilters);
        filterOptions = drilldown.insights.filterOptions;
      } catch (error) {
        drilldownError = error instanceof Error ? error.message : "Dashboard drilldown data is unavailable.";
      }
    } else if (safeFilters.scope !== "global") {
      try {
        filterOptions = await getDashboardFilterOptions(safeFilters);
      } catch (error) {
        drilldownError = error instanceof Error ? error.message : "Dashboard filter options are unavailable.";
      }
    }

    return {
      globalOverview,
      overview: overview ?? createOverviewFromGlobalOverview(globalOverview),
      guilds,
      filterOptions,
      drilldown,
      drilldownError,
      usingDemoData: false,
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : "Dashboard API is unavailable.";
    return createDemoDashboardData(safeFilters, message);
  }
}

async function getDashboardFilterOptions(filters: DashboardFilters): Promise<DashboardFilterOptions> {
  const optionScope = filters.guildId ? "server" : "global";
  const insights = await dashboardRequest<DashboardInsightsResponse>("/insights", {
    guildId: filters.guildId,
    days: filters.days,
    startDate: filters.startDate,
    endDate: filters.endDate,
    scope: optionScope,
    view: "summary",
    sortDirection: filters.sortDirection,
    minActivity: filters.minActivity,
  });

  return insights.filterOptions;
}

export async function getQuoteDetails(quoteId: number): Promise<DashboardQuoteDetailsResponse | null> {
  try {
    return await dashboardRequest<DashboardQuoteDetailsResponse>(`/quotes/${quoteId}`);
  } catch {
    return null;
  }
}

export async function getQuoteApprovalDetails(approvalId: number): Promise<DashboardQuoteApprovalRequestItem | null> {
  try {
    return await dashboardRequest<DashboardQuoteApprovalRequestItem>(`/quote-approvals/${approvalId}`);
  } catch {
    return null;
  }
}

async function getGuildSummaries(fetchFull: boolean): Promise<DashboardGuildSummary[]> {
  if (fetchFull) {
    return dashboardRequest<DashboardGuildSummary[]>("/guilds");
  }

  try {
    return await dashboardRequest<DashboardGuildSummary[]>("/guild-options");
  } catch {
    return dashboardRequest<DashboardGuildSummary[]>("/guilds");
  }
}

function createOverviewFromGlobalOverview(globalOverview: DashboardGlobalOverviewResponse): DashboardOverviewResponse {
  return {
    generatedAtUtc: globalOverview.generatedAtUtc,
    startedAtUtc: globalOverview.generatedAtUtc,
    uptimeSeconds: 0,
    system: {
      guilds: globalOverview.totals.totalServers,
      users: globalOverview.totals.totalKnownUsers,
      stocks: 0,
    },
    activity: {
      totalMessages: globalOverview.totals.totalTrackedMessages,
      totalXp: globalOverview.totals.totalXpGenerated,
      activeUsersLast30Days: 0,
      messagesLast30Days: 0,
      xpLast30Days: 0,
      lastActivityAtUtc: null,
    },
    quotes: {
      approved: globalOverview.totals.totalApprovedQuotes,
      pending: globalOverview.totals.pendingQuotes,
      removed: 0,
      totalScores: 0,
    },
    economy: {
      totalBalance: globalOverview.totals.totalEconomyBalance,
      stockPortfolioValue: Math.max(
        0,
        globalOverview.totals.totalEstimatedNetWorth - globalOverview.totals.totalEconomyBalance,
      ),
    },
    logs: {
      total: 0,
      last24Hours: globalOverview.totals.recentWarningsOrErrors,
      lastLogAtUtc: null,
    },
  };
}

function createEmptyGlobalOverview(days: number): DashboardGlobalOverviewResponse {
  return {
    generatedAtUtc: new Date().toISOString(),
    days,
    totals: {
      totalServers: 0,
      totalKnownUsers: 0,
      totalTrackedMessages: 0,
      totalXpGenerated: 0,
      latestDayMessages: 0,
      latestDayXpGenerated: 0,
      totalQuotes: 0,
      totalApprovedQuotes: 0,
      pendingQuotes: 0,
      pendingQuoteApprovals: 0,
      totalEconomyBalance: 0,
      totalEstimatedNetWorth: 0,
      ubiPoolSize: 0,
      slotsVaultSize: 0,
      totalTransactions: 0,
      totalButtonPresses: 0,
      activeReminders: 0,
      recentWarningsOrErrors: 0,
    },
    highlights: {
      mostActiveServersToday: [],
      mostActiveServersThisWeek: [],
      mostActiveServersThisMonth: [],
      mostActiveServersAllTime: [],
      mostActiveServersSelectedWindow: [],
      biggestXpGainers: [],
      richestUsersByBalance: [],
      richestUsersByNetWorth: [],
      biggestStockGainers: [],
      biggestStockLosers: [],
      mostPopularQuotes: [],
      mostActiveChannels: [],
      mostActiveUsers: [],
      recentlyCreatedUsers: [],
      recentlyCreatedServers: [],
      recentlyCreatedQuotes: [],
      recentlyCreatedStocks: [],
    },
    visuals: {
      activity: [],
      stackedServerActivity: [],
      calendarActivity: [],
      hourByWeekdayActivity: [],
      transactionTypes: [],
    },
    feeds: {
      recentEconomyEvents: [],
      recentBotHealthEvents: [],
    },
  };
}

function createEmptyFilterOptions(): DashboardFilterOptions {
  return {
    users: [],
    channels: [],
  };
}

async function getDashboardDrilldownData(filters: DashboardFilters): Promise<DashboardDrilldownData> {
  const view = filters.view ?? "summary";
  const isServerSummary = filters.scope === "server" && view === "summary" && Boolean(filters.guildId);
  const isUserSummary = filters.scope === "user" && view === "summary" && Boolean(filters.userId);
  const insightView = isServerSummary || isUserSummary ? undefined : view;
  const shouldFetchActivity = view === "activity" || isServerSummary || isUserSummary;
  const shouldFetchLeaderboards = view === "users" || isServerSummary || isUserSummary;
  const [activity, xpLeaderboard, messageLeaderboard, insights] = await Promise.all([
    shouldFetchActivity
      ? dashboardRequest<DashboardActivitySeriesResponse>("/activity", {
          guildId: filters.guildId,
          userId: filters.userId,
          channelId: filters.channelId,
          days: filters.days,
          startDate: filters.startDate,
          endDate: filters.endDate,
        })
      : Promise.resolve(createEmptyActivitySeries(filters.guildId, filters.days)),
    shouldFetchLeaderboards
      ? dashboardRequest<DashboardLeaderboardResponse>("/leaderboard", {
          guildId: filters.guildId,
          userId: filters.userId,
          channelId: filters.channelId,
          metric: "xp",
          days: filters.days,
          startDate: filters.startDate,
          endDate: filters.endDate,
          limit: 10,
        })
      : Promise.resolve(createEmptyLeaderboard(filters.guildId, "xp", filters.days)),
    shouldFetchLeaderboards
      ? dashboardRequest<DashboardLeaderboardResponse>("/leaderboard", {
          guildId: filters.guildId,
          userId: filters.userId,
          channelId: filters.channelId,
          metric: "messages",
          days: filters.days,
          startDate: filters.startDate,
          endDate: filters.endDate,
          limit: 10,
        })
      : Promise.resolve(createEmptyLeaderboard(filters.guildId, "messages", filters.days)),
    dashboardRequest<DashboardDrilldownData["insights"]>("/insights", {
      guildId: filters.guildId,
      userId: filters.userId,
      channelId: filters.channelId,
      days: filters.days,
      startDate: filters.startDate,
      endDate: filters.endDate,
      scope: filters.scope,
      view: insightView,
      sortDirection: filters.sortDirection,
      minActivity: filters.minActivity,
    }),
  ]);

  return {
    activity,
    xpLeaderboard,
    messageLeaderboard,
    quotes: createEmptyQuotePage(),
    insights,
  };
}

function createEmptyActivitySeries(guildId: number | undefined, days: number): DashboardActivitySeriesResponse {
  return {
    guildId: guildId ?? null,
    days,
    points: [],
  };
}

function createEmptyLeaderboard(
  guildId: number | undefined,
  metric: DashboardLeaderboardResponse["metric"],
  days: number,
): DashboardLeaderboardResponse {
  return {
    guildId: guildId ?? null,
    metric,
    days,
    limit: 10,
    items: [],
  };
}

function createEmptyQuotePage(): DashboardQuotePageResponse {
  return {
    page: 1,
    totalPages: 0,
    total: 0,
    items: [],
  };
}

async function dashboardRequest<T>(
  path: string,
  params: Record<string, string | number | boolean | null | undefined> = {},
): Promise<T> {
  const apiUrl = process.env.DASHBOARD_API_URL || defaultApiUrl;
  const url = new URL(`api/dashboard${path}`, ensureTrailingSlash(apiUrl));

  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") {
      url.searchParams.set(key, String(value));
    }
  }

  const headers = new Headers({
    Accept: "application/json",
  });

  const apiKey = process.env.DASHBOARD_API_KEY;
  if (apiKey) {
    headers.set("X-Dashboard-Key", apiKey);
  }

  const cacheKey = `${apiUrl}|${path}|${url.searchParams.toString()}|${apiKey ? "auth" : "anon"}`;
  const now = Date.now();
  pruneDashboardResponseCache(now);
  const cached = dashboardResponseCache.get(cacheKey);
  if (cached && cached.expiresAt > now) {
    return cached.value as T;
  }

  const response = await withDashboardTimeout(
    fetch(url, {
      headers,
      cache: "no-store",
    }),
    path,
  );

  if (!response.ok) {
    throw new Error(`Dashboard API returned ${response.status} for ${path}.`);
  }

  const value = (await response.json()) as T;
  const fetchedAt = Date.now();
  pruneDashboardResponseCache(fetchedAt);
  dashboardResponseCache.set(cacheKey, {
    expiresAt: fetchedAt + dashboardResponseCacheTtlMs,
    value,
  });

  return value;
}

function pruneDashboardResponseCache(now: number) {
  for (const [key, cached] of dashboardResponseCache) {
    if (cached.expiresAt <= now) {
      dashboardResponseCache.delete(key);
    }
  }

  while (dashboardResponseCache.size >= dashboardResponseCacheMaxEntries) {
    const oldestKey = dashboardResponseCache.keys().next().value;
    if (oldestKey === undefined) {
      break;
    }

    dashboardResponseCache.delete(oldestKey);
  }
}

function ensureTrailingSlash(value: string) {
  return value.endsWith("/") ? value : `${value}/`;
}

function withDashboardTimeout<T>(promise: Promise<T>, path: string): Promise<T> {
  let timeout: ReturnType<typeof setTimeout> | undefined;
  const timeoutPromise = new Promise<T>((_, reject) => {
    timeout = setTimeout(
      () => reject(new Error(`Dashboard API timed out for ${path}.`)),
      dashboardRequestTimeoutMs,
    );
  });

  return Promise.race([promise, timeoutPromise]).finally(() => {
    if (timeout) {
      clearTimeout(timeout);
    }
  });
}
