import type {
  DashboardActivityDerivedPoint,
  DashboardActivityPoint,
  DashboardButtonGamePoint,
  DashboardData,
  DashboardEconomyFlowPoint,
  DashboardFilters,
  DashboardGuildSummary,
  DashboardHeatmapCell,
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

  const derivedPoints = buildDerivedActivity(points);
  const heatmap = buildHeatmap();
  const economyFlow = buildEconomyFlow(days, now);
  const buttonDaily = buildButtonDaily(days, now);
  const totalMessages = points.reduce((sum, point) => sum + point.messages, 0);
  const totalXp = points.reduce((sum, point) => sum + point.xp, 0);

  const xpItems = [
    ["vyc", 38240, 9],
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
  }));

  const userOptions = xpItems.map((item) => ({
    userId: item.userId,
    discordId: item.discordId,
    username: item.username,
  }));
  const channelRows = [
    ["general", "1165553796223602001", 8420, 70240, 38],
    ["bot-commands", "1165553796223602002", 3890, 22520, 29],
    ["quotes", "1165553796223602003", 1022, 7210, 18],
    ["market", "1165553796223602004", 874, 6200, 13],
  ] as const;
  const channelOptions = channelRows.map(([name, discordId]) => ({
    discordId,
    name,
  }));

  const messageItems = [
    ["ana", 2420],
    ["vyc", 2288],
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
  }));

  const quotes = [
    {
      id: 404,
      guildId: selectedGuildId ?? 1,
      userId: 1,
      author: "vyc",
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
  ];

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
      items: xpItems,
    },
    messageLeaderboard: {
      guildId: selectedGuildId,
      metric: "messages",
      days,
      limit: 10,
      items: messageItems,
    },
    quotes: {
      page: 1,
      totalPages: 1,
      total: 4,
      items: quotes,
    },
    insights: {
      guildId: selectedGuildId,
      userId: filters.userId ?? null,
      channelId: filters.channelId ?? null,
      days,
      scope: filters.scope,
      sortDirection: filters.sortDirection,
      minActivity: filters.minActivity,
      activity: {
        messages: totalMessages,
        xp: totalXp,
        activeUsers: 48,
        activeChannels: 9,
        averageMessageLength: 58.7,
        messagesPerActiveUser: 382.1,
        xpPerMessage: 8.4,
        peakHourUtc: 20,
        trendPercent: 12.8,
        points: derivedPoints,
      },
      channels: channelRows.map(([name, discordId, messages, xp, activeUsers], index) => ({
        rank: index + 1,
        discordId,
        name,
        messages,
        xp,
        activeUsers,
        averageMessageLength: 44 + index * 6.8,
        lastActivityAtUtc: now.toISOString(),
      })),
      users: xpItems.map((item, index) => ({
        rank: index + 1,
        userId: item.userId,
        discordId: item.discordId,
        username: item.username,
        messages: 2200 - index * 230,
        xp: item.value,
        level: item.level ?? 0,
        quotes: 21 - index * 3,
        balance: 18500 - index * 1450,
        stockPortfolioValue: 9600 - index * 780,
        buttonScore: 4450 - index * 390,
        averageMessageLength: 62 - index * 3.2,
        lastActivityAtUtc: item.lastActivityAtUtc,
      })),
      heatmap,
      quotes: {
        approved: 118,
        pending: 7,
        removed: 4,
        approvalRequests: 34,
        pendingApprovalRequests: 5,
        averageScore: 7.8,
        statuses: [
          { status: "Approved", count: 118 },
          { status: "Pending", count: 7 },
          { status: "Removed", count: 4 },
        ],
        authors: [
          { userId: 1, discordId: "900000000000", username: "vyc", quotes: 32, score: 184 },
          { userId: 2, discordId: "900000000001", username: "ana", quotes: 27, score: 161 },
          { userId: 3, discordId: "900000000002", username: "mira", quotes: 19, score: 93 },
        ],
        scoreHistogram: [
          { label: "< 0", count: 3 },
          { label: "0", count: 11 },
          { label: "1-5", count: 46 },
          { label: "6-15", count: 55 },
          { label: "16+", count: 14 },
        ],
        recentPending: [
          {
            id: 441,
            guildId: selectedGuildId ?? 1,
            userId: 4,
            author: "radu",
            content: "approval queues are just democracy with suspense",
            insertedAtUtc: now.toISOString(),
            approved: false,
            removed: false,
            score: 3,
          },
        ],
      },
      economy: {
        cashBalance: 184320,
        portfolioValue: 96780,
        netWorth: 281100,
        activeWallets: 56,
        activeTraders: 18,
        transactionVolume: 74250,
        fees: 1240,
        dailyFlow: economyFlow,
        transactionTypes: [
          { label: "Stock buy", value: 28400 },
          { label: "Stock sell", value: 18100 },
          { label: "Transfer", value: 12750 },
          { label: "Slots win", value: 9200 },
          { label: "Donation", value: 5800 },
        ],
        moneyFlows: [
          { source: "Wallets", target: "Stock market", value: 28400 },
          { source: "Stock market", target: "Wallets", value: 18100 },
          { source: "Wallets", target: "Member transfers", value: 12750 },
          { source: "Slots", target: "Wallets", value: 9200 },
        ],
        wealthLeaders: xpItems.map((item, index) => ({
          rank: index + 1,
          userId: item.userId,
          discordId: item.discordId,
          username: item.username,
          balance: 22400 - index * 1700,
          stockPortfolioValue: 15100 - index * 930,
          netWorth: 37500 - index * 2630,
        })),
      },
      stocks: {
        stocks: 86,
        marketValue: 96780,
        averageDailyChangePercent: 2.4,
        winners: [
          { stockId: 12, entityType: "User", name: "ana", price: 184.42, dailyChangePercent: 9.8, holdingValue: 12400 },
          { stockId: 33, entityType: "Channel", name: "market", price: 142.14, dailyChangePercent: 7.1, holdingValue: 8100 },
          { stockId: 6, entityType: "Server", name: "Morpheus Lab", price: 121.9, dailyChangePercent: 5.6, holdingValue: 17400 },
        ],
        losers: [
          { stockId: 44, entityType: "User", name: "alex", price: 88.3, dailyChangePercent: -6.2, holdingValue: 2300 },
          { stockId: 51, entityType: "Channel", name: "quotes", price: 94.72, dailyChangePercent: -3.8, holdingValue: 5100 },
        ],
        entityTypes: [
          { label: "User", value: 61 },
          { label: "Channel", value: 18 },
          { label: "Server", value: 7 },
        ],
      },
      buttonGame: {
        presses: 924,
        score: 48220,
        averageScore: 52.2,
        lastPressAtUtc: now.toISOString(),
        daily: buttonDaily,
        leaders: xpItems.slice(0, 4).map((item, index) => ({
          rank: index + 1,
          userId: item.userId,
          discordId: item.discordId,
          username: item.username,
          presses: 180 - index * 26,
          score: 9440 - index * 880,
          lastPressAtUtc: now.toISOString(),
        })),
      },
      operations: {
        reminders: {
          pending: 14,
          overdue: 2,
          dueNext24Hours: 5,
          upcoming: [
            {
              id: 18,
              channelId: "1165553796223602001",
              text: "ship dashboard review",
              user: "vyc",
              dueDateUtc: new Date(now.getTime() + 1000 * 60 * 80).toISOString(),
            },
            {
              id: 19,
              channelId: "1165553796223602004",
              text: "check stock reset",
              user: "ana",
              dueDateUtc: new Date(now.getTime() + 1000 * 60 * 60 * 7).toISOString(),
            },
          ],
        },
        moderation: {
          pendingTemporaryBans: 3,
          overdueTemporaryBans: 1,
          completedLast30Days: 11,
          pending: [
            {
              id: 8,
              guildId: guilds[0].discordId,
              userId: "882222331111",
              reason: "temporary cooldown",
              expiresAtUtc: new Date(now.getTime() + 1000 * 60 * 60 * 3).toISOString(),
            },
          ],
        },
        logSeverities: [
          { severity: "Info", count: 246 },
          { severity: "Warning", count: 16 },
          { severity: "Error", count: 3 },
        ],
        logTimeline: points.map((point, index) => ({
          dateUtc: point.dateUtc,
          total: Math.round(point.messages / 3),
          warnings: index % 6 === 0 ? 3 : index % 4,
          errors: index % 13 === 0 ? 1 : 0,
        })),
        recentLogs: [
          {
            id: 921,
            severity: "Info",
            message: "Quartz Job - Stock update completed for 86 entities",
            version: "1.7.0",
            insertedAtUtc: now.toISOString(),
          },
          {
            id: 920,
            severity: "Warning",
            message: "Approval message update retried for quote 441",
            version: "1.7.0",
            insertedAtUtc: new Date(now.getTime() - 1000 * 60 * 32).toISOString(),
          },
        ],
      },
      settings: guilds.map((guild) => ({
        guildId: guild.id,
        guildName: guild.name,
        prefix: "m!",
        levelUpMessages: guild.id === 1,
        levelUpQuotes: true,
        useGlobalQuotes: guild.id === 2,
        welcomeMessages: guild.id === 1,
        useActivityRoles: guild.id === 1,
        quoteAddRequiredApprovals: 5,
        quoteRemoveRequiredApprovals: 5,
      })),
      filterOptions: {
        users: userOptions,
        channels: channelOptions,
      },
    },
  };
}

function buildDerivedActivity(points: DashboardActivityPoint[]): DashboardActivityDerivedPoint[] {
  let cumulativeMessages = 0;
  let cumulativeXp = 0;

  return points.map((point, index) => {
    cumulativeMessages += point.messages;
    cumulativeXp += point.xp;
    const rollingWindow = points.slice(Math.max(0, index - 6), index + 1);

    return {
      dateUtc: point.dateUtc,
      messages: point.messages,
      xp: point.xp,
      activeUsers: point.activeUsers,
      rollingMessages: Math.round(
        (rollingWindow.reduce((sum, item) => sum + item.messages, 0) / rollingWindow.length) * 10,
      ) / 10,
      cumulativeMessages,
      cumulativeXp,
    };
  });
}

function buildHeatmap(): DashboardHeatmapCell[] {
  const labels = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
  const cells: DashboardHeatmapCell[] = [];

  for (let day = 0; day < 7; day++) {
    for (let hour = 0; hour < 24; hour++) {
      const eveningBoost = hour >= 18 && hour <= 23 ? 3 : 1;
      const weekdayBoost = day >= 1 && day <= 5 ? 1.2 : 0.8;
      const messages = Math.round(((Math.sin((hour + day) / 3) + 1.2) * 12 + day) * eveningBoost * weekdayBoost);
      cells.push({
        dayOfWeek: day,
        dayLabel: labels[day],
        hourUtc: hour,
        messages,
        xp: messages * 8,
        activeUsers: Math.max(0, Math.round(messages / 12)),
      });
    }
  }

  return cells;
}

function buildEconomyFlow(days: number, now: Date): DashboardEconomyFlowPoint[] {
  return Array.from({ length: days }, (_, index) => {
    const date = new Date(now);
    date.setUTCHours(0, 0, 0, 0);
    date.setUTCDate(date.getUTCDate() - (days - index - 1));
    const inflow = Math.round(600 + (Math.sin(index / 3) + 1) * 390 + (index % 4) * 120);
    const outflow = Math.round(480 + (Math.cos(index / 4) + 1) * 340 + (index % 5) * 95);

    return {
      dateUtc: date.toISOString(),
      inflow,
      outflow,
      net: inflow - outflow,
    };
  });
}

function buildButtonDaily(days: number, now: Date): DashboardButtonGamePoint[] {
  let cumulativeScore = 0;
  const points = Array.from({ length: days }, (_, index) => {
    const date = new Date(now);
    date.setUTCHours(0, 0, 0, 0);
    date.setUTCDate(date.getUTCDate() - (days - index - 1));
    const presses = Math.round(18 + (Math.sin(index / 2.2) + 1.2) * 12);
    const score = presses * Math.round(42 + (index % 6) * 4);
    cumulativeScore += score;

    return {
      dateUtc: date.toISOString(),
      presses,
      score,
      activeUsers: Math.max(1, Math.round(presses / 8)),
      rollingPresses: 0,
      cumulativeScore,
    };
  });

  return points.map((point, index) => {
    const rollingWindow = points.slice(Math.max(0, index - 6), index + 1);
    return {
      ...point,
      rollingPresses:
        Math.round((rollingWindow.reduce((sum, item) => sum + item.presses, 0) / rollingWindow.length) * 10) /
        10,
    };
  });
}
