import { createDemoDashboardData } from "@/lib/demo-data";
import type {
  DashboardActivitySeriesResponse,
  DashboardData,
  DashboardFilters,
  DashboardGuildSummary,
  DashboardLeaderboardResponse,
  DashboardOverviewResponse,
  DashboardQuotePageResponse,
} from "@/lib/types";
import { clamp } from "@/lib/utils";

const defaultApiUrl = "http://127.0.0.1:5267";

export async function getDashboardData(filters: DashboardFilters): Promise<DashboardData> {
  const safeFilters = {
    guildId: filters.guildId,
    userId: filters.userId,
    channelId: filters.channelId,
    days: clamp(filters.days, 1, 90),
    scope: filters.scope,
    sortDirection: filters.sortDirection,
    minActivity: clamp(filters.minActivity, 0, 100000),
  };

  try {
    const [overview, guilds, activity, xpLeaderboard, messageLeaderboard, quotes, insights] = await Promise.all([
      dashboardRequest<DashboardOverviewResponse>("/overview"),
      dashboardRequest<DashboardGuildSummary[]>("/guilds"),
      dashboardRequest<DashboardActivitySeriesResponse>("/activity", {
        guildId: safeFilters.guildId,
        userId: safeFilters.userId,
        channelId: safeFilters.channelId,
        days: safeFilters.days,
      }),
      dashboardRequest<DashboardLeaderboardResponse>("/leaderboard", {
        guildId: safeFilters.guildId,
        userId: safeFilters.userId,
        channelId: safeFilters.channelId,
        metric: "xp",
        days: safeFilters.days,
        limit: 10,
      }),
      dashboardRequest<DashboardLeaderboardResponse>("/leaderboard", {
        guildId: safeFilters.guildId,
        userId: safeFilters.userId,
        channelId: safeFilters.channelId,
        metric: "messages",
        days: safeFilters.days,
        limit: 10,
      }),
      dashboardRequest<DashboardQuotePageResponse>("/quotes", {
        guildId: safeFilters.guildId,
        page: 1,
        sort: "top",
        approvedOnly: true,
      }),
      dashboardRequest<DashboardData["insights"]>("/insights", {
        guildId: safeFilters.guildId,
        userId: safeFilters.userId,
        channelId: safeFilters.channelId,
        days: safeFilters.days,
        scope: safeFilters.scope,
        sortDirection: safeFilters.sortDirection,
        minActivity: safeFilters.minActivity,
      }),
    ]);

    return {
      overview,
      guilds,
      activity,
      xpLeaderboard,
      messageLeaderboard,
      quotes,
      insights,
      usingDemoData: false,
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : "Dashboard API is unavailable.";
    return createDemoDashboardData(safeFilters, message);
  }
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

  const response = await fetch(url, {
    headers,
    cache: "no-store",
  });

  if (!response.ok) {
    throw new Error(`Dashboard API returned ${response.status} for ${path}.`);
  }

  return (await response.json()) as T;
}

function ensureTrailingSlash(value: string) {
  return value.endsWith("/") ? value : `${value}/`;
}
