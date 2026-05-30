import type {
  DashboardActivityPoint,
  DashboardData,
  DashboardFilters,
  DashboardGuildSummary,
} from "@/lib/types";

export function createDemoDashboardData(filters: DashboardFilters, error?: string): DashboardData {
  const now = new Date();
  const selectedGuildId = filters.guildId ?? null;
  const days = filters.days;

  const guilds: DashboardGuildSummary[] = [
    {
      id: 1,
      discordId: "1165553796223602708",
      name: "Morpheus Lab",
      insertedAtUtc: "2025-08-18T20:40:00Z",
      trackedUsers: 42,
      messages: 183240,
      xp: 921400,
      approvedQuotes: 314,
    },
    {
      id: 2,
      discordId: "927533069211172885",
      name: "Night Shift",
      insertedAtUtc: "2025-10-02T18:12:00Z",
      trackedUsers: 19,
      messages: 84210,
      xp: 384900,
      approvedQuotes: 122,
    },
  ];

  const points: DashboardActivityPoint[] = Array.from({ length: days }, (_, index) => {
    const date = new Date(now);
    date.setUTCHours(0, 0, 0, 0);
    date.setUTCDate(date.getUTCDate() - (days - index - 1));

    const wave = Math.sin(index / 2.6) + 1.5;
    const messages = Math.round(95 + wave * 46 + (index % 5) * 9);

    return {
      dateUtc: date.toISOString(),
      messages,
      xp: Math.round(messages * (7.8 + (index % 4))),
      activeUsers: Math.round(12 + wave * 4 + (index % 3)),
      averageMessageLength: Math.round((42 + wave * 11) * 10) / 10,
    };
  });

  return {
    usingDemoData: true,
    error,
    guilds,
    overview: {
      generatedAtUtc: now.toISOString(),
      startedAtUtc: new Date(now.getTime() - 1000 * 60 * 60 * 42).toISOString(),
      uptimeSeconds: 151200,
      system: {
        guilds: guilds.length,
        users: 61,
        stocks: 86,
      },
      activity: {
        totalMessages: 267450,
        totalXp: 1306300,
        activeUsersLast30Days: 48,
        messagesLast30Days: 18342,
        xpLast30Days: 142880,
        lastActivityAtUtc: now.toISOString(),
      },
      quotes: {
        approved: 436,
        pending: 7,
        removed: 18,
        totalScores: 1284,
      },
      economy: {
        totalBalance: 184320,
        stockPortfolioValue: 96780,
      },
      logs: {
        total: 18240,
        last24Hours: 122,
        lastLogAtUtc: now.toISOString(),
      },
    },
    activity: {
      guildId: selectedGuildId,
      days,
      points,
    },
    xpLeaderboard: {
      guildId: selectedGuildId,
      metric: "xp",
      days,
      limit: 10,
      items: [
        ["vycto", 38240, 9],
        ["ana", 31120, 8],
        ["radu", 29880, 8],
        ["mira", 22450, 7],
        ["alex", 19820, 7],
      ].map(([username, value, level], index) => ({
        rank: index + 1,
        userId: index + 1,
        discordId: String(900000000000 + index),
        username: String(username),
        value: Number(value),
        level: Number(level),
        lastActivityAtUtc: now.toISOString(),
      })),
    },
    messageLeaderboard: {
      guildId: selectedGuildId,
      metric: "messages",
      days,
      limit: 10,
      items: [
        ["ana", 2420],
        ["vycto", 2288],
        ["mira", 1940],
        ["radu", 1842],
        ["alex", 1440],
      ].map(([username, value], index) => ({
        rank: index + 1,
        userId: index + 11,
        discordId: String(800000000000 + index),
        username: String(username),
        value: Number(value),
        level: null,
        lastActivityAtUtc: now.toISOString(),
      })),
    },
    quotes: {
      page: 1,
      totalPages: 1,
      total: 4,
      items: [
        {
          id: 404,
          guildId: selectedGuildId ?? 1,
          userId: 1,
          author: "vycto",
          content: "the bot has become sentient but only for charts",
          insertedAtUtc: "2026-05-25T13:00:00Z",
          approved: true,
          removed: false,
          score: 22,
        },
        {
          id: 388,
          guildId: selectedGuildId ?? 1,
          userId: 2,
          author: "ana",
          content: "that economy command is just wall street with better memes",
          insertedAtUtc: "2026-05-21T20:30:00Z",
          approved: true,
          removed: false,
          score: 18,
        },
        {
          id: 381,
          guildId: selectedGuildId ?? 2,
          userId: 3,
          author: "mira",
          content: "xp is a lifestyle choice at this point",
          insertedAtUtc: "2026-05-20T18:45:00Z",
          approved: true,
          removed: false,
          score: 13,
        },
      ],
    },
  };
}
