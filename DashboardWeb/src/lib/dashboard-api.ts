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
const dashboardRequestTimeoutMs = 3000;
const dashboardLongRequestTimeoutMs = 15000;
const dashboardResponseCacheFreshMs = 15000;
const dashboardResponseCacheStaleMs = 120000;
const dashboardSelectorCacheFreshMs = 300000;
const dashboardSelectorCacheStaleMs = 900000;
const dashboardDetailCacheFreshMs = 60000;
const dashboardDetailCacheStaleMs = 300000;
const dashboardResponseCacheMaxEntries = 256;
const dashboardResponseCache = new Map<string, { freshUntil: number; staleUntil: number; value: unknown }>();
const dashboardInFlightRequests = new Map<string, Promise<unknown>>();

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
        (safeFilters.view === "quotes" ||
          safeFilters.view === "economy" ||
          safeFilters.view === "stocks" ||
          safeFilters.view === "operations" ||
          safeFilters.view === "settings"));
    const shouldFetchGlobalOverview = safeFilters.scope === "global";
    const globalOverviewView = shouldFetchGlobalOverview ? safeFilters.view : "summary";
    const shouldFetchOverview =
      hasScopedSelection &&
      (safeFilters.view === "activity" ||
        (safeFilters.view === "summary" && safeFilters.scope === "channel"));
    const shouldFetchFullGuilds = shouldFetchDrilldown && safeFilters.view === "settings";
    const globalOverviewRequest = shouldFetchGlobalOverview
      ? dashboardRequest<DashboardGlobalOverviewResponse>("/global-overview", {
          days: safeFilters.days,
          startDate: safeFilters.startDate,
          endDate: safeFilters.endDate,
          view: globalOverviewView,
        })
      : Promise.resolve(createEmptyGlobalOverview(safeFilters.days));
    const drilldownDataRequest = shouldFetchDrilldown
      ? getDashboardDrilldownData(safeFilters)
          .then((drilldown) => ({
            drilldown,
            drilldownError: undefined,
            filterOptions: drilldown.insights.filterOptions,
          }))
          .catch((error) => ({
            drilldown: null,
            drilldownError: error instanceof Error ? error.message : "Dashboard drilldown data is unavailable.",
            filterOptions: createEmptyFilterOptions(),
          }))
      : safeFilters.scope !== "global"
        ? getDashboardFilterOptions(safeFilters)
            .then((filterOptions) => ({
              drilldown: null,
              drilldownError: undefined,
              filterOptions,
            }))
            .catch((error) => ({
              drilldown: null,
              drilldownError: error instanceof Error ? error.message : "Dashboard filter options are unavailable.",
              filterOptions: createEmptyFilterOptions(),
            }))
        : Promise.resolve({
          drilldown: null,
          drilldownError: undefined,
          filterOptions: createEmptyFilterOptions(),
        });
    const [globalOverview, overview, guilds, drilldownData] = await Promise.all([
      globalOverviewRequest,
      shouldFetchOverview
        ? dashboardRequest<DashboardOverviewResponse>("/overview").catch(() => null)
        : Promise.resolve<DashboardOverviewResponse | null>(null),
      getGuildSummaries(shouldFetchFullGuilds),
      drilldownDataRequest,
    ]);

    return {
      globalOverview,
      overview: overview ?? createOverviewFromGlobalOverview(globalOverview),
      guilds,
      filterOptions: drilldownData.filterOptions,
      drilldown: drilldownData.drilldown,
      drilldownError: drilldownData.drilldownError,
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
  } catch (error) {
    if (shouldTryFullGuildFallback(error)) {
      return dashboardRequest<DashboardGuildSummary[]>("/guilds");
    }

    return [];
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
  const insightView = view;
  const shouldFetchActivity = filters.scope !== "global" && view === "activity";
  const shouldFetchLeaderboards = view === "users" || isServerSummary;
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
  if (cached && cached.freshUntil > now) {
    return cached.value as T;
  }

  if (cached && cached.staleUntil > now) {
    void refreshDashboardCache<T>(cacheKey, url, headers, path, params).catch(() => undefined);
    return cached.value as T;
  }

  return refreshDashboardCache<T>(cacheKey, url, headers, path, params);
}

async function refreshDashboardCache<T>(
  cacheKey: string,
  url: URL,
  headers: Headers,
  path: string,
  params: Record<string, string | number | boolean | null | undefined> = {},
): Promise<T> {
  const inFlight = dashboardInFlightRequests.get(cacheKey);
  if (inFlight) {
    return inFlight as Promise<T>;
  }

  const timeoutMs = getDashboardRequestTimeoutMs(path, params);
  const request = fetchDashboardJson<T>(url, headers, path, timeoutMs)
    .then((value) => {
      const fetchedAt = Date.now();
      const cachePolicy = getDashboardCachePolicy(path);
      pruneDashboardResponseCache(fetchedAt);
      dashboardResponseCache.set(cacheKey, {
        freshUntil: fetchedAt + cachePolicy.freshMs,
        staleUntil: fetchedAt + cachePolicy.staleMs,
        value,
      });

      return value;
    })
    .finally(() => {
      dashboardInFlightRequests.delete(cacheKey);
    });

  dashboardInFlightRequests.set(cacheKey, request);
  return request;
}

async function fetchDashboardJson<T>(url: URL, headers: Headers, path: string, timeoutMs: number): Promise<T> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);

  let response: Response;
  try {
    response = await fetch(url, {
      headers,
      cache: "no-store",
      signal: controller.signal,
    });
  } catch (error) {
    if (controller.signal.aborted) {
      throw new Error(`Dashboard API timed out for ${path}.`);
    }

    throw error;
  } finally {
    clearTimeout(timeout);
  }

  if (!response.ok) {
    throw new Error(`Dashboard API returned ${response.status} for ${path}.`);
  }

  return (await response.json()) as T;
}

function pruneDashboardResponseCache(now: number) {
  for (const [key, cached] of dashboardResponseCache) {
    if (cached.staleUntil <= now) {
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

function getDashboardRequestTimeoutMs(
  path: string,
  params: Record<string, string | number | boolean | null | undefined>,
) {
  if (!usesDateWindowTimeout(path)) {
    return dashboardRequestTimeoutMs;
  }

  return hasLongDashboardDateWindow(params) ? dashboardLongRequestTimeoutMs : dashboardRequestTimeoutMs;
}

function usesDateWindowTimeout(path: string) {
  return path === "/global-overview" ||
    path === "/insights" ||
    path === "/activity" ||
    path === "/leaderboard";
}

function hasLongDashboardDateWindow(params: Record<string, string | number | boolean | null | undefined>) {
  const days = typeof params.days === "number"
    ? params.days
    : Number.parseInt(String(params.days ?? ""), 10);
  if (Number.isFinite(days) && days > 365) {
    return true;
  }

  const startDate = typeof params.startDate === "string" ? Date.parse(params.startDate) : Number.NaN;
  const endDate = typeof params.endDate === "string" ? Date.parse(params.endDate) : Number.NaN;
  if (Number.isFinite(startDate) && Number.isFinite(endDate)) {
    const spanDays = Math.abs(endDate - startDate) / 86400000;
    if (spanDays > 365) {
      return true;
    }
  }

  return false;
}

function getDashboardCachePolicy(path: string) {
  if (path === "/guilds" || path === "/guild-options") {
    return {
      freshMs: dashboardSelectorCacheFreshMs,
      staleMs: dashboardSelectorCacheStaleMs,
    };
  }

  if (path.startsWith("/quotes/") || path.startsWith("/quote-approvals/")) {
    return {
      freshMs: dashboardDetailCacheFreshMs,
      staleMs: dashboardDetailCacheStaleMs,
    };
  }

  return {
    freshMs: dashboardResponseCacheFreshMs,
    staleMs: dashboardResponseCacheStaleMs,
  };
}

function shouldTryFullGuildFallback(error: unknown) {
  return error instanceof Error &&
    (error.message.includes("returned 404") || error.message.includes("returned 405"));
}
