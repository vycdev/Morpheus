import type {
  DashboardActivityDerivedPoint,
  DashboardActivityAnalytics,
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
  const selectedGuild = guilds.find((guild) => guild.id === selectedGuildId) ?? guilds[0];

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
  const stackedServerActivity = buildStackedServerActivity(days, now, guilds);
  const calendarActivity = derivedPoints.map((point) => ({
    dateUtc: point.dateUtc,
    messages: point.messages,
    xp: point.xp,
    activeUsers: point.activeUsers,
  }));
  const totalMessages = points.reduce((sum, point) => sum + point.messages, 0);
  const totalXp = points.reduce((sum, point) => sum + point.xp, 0);
  const latestPoint = points.at(-1);

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
  const demoUserProfile = filters.userId
    ? buildDemoUserProfile(filters.userId, xpItems, guilds, channelRows, derivedPoints, heatmap, economyFlow, buttonDaily, now)
    : null;
  const activityAnalytics = buildDemoActivityAnalytics(
    days,
    now,
    guilds,
    channelRows,
    xpItems,
    derivedPoints,
    heatmap,
  );

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
  const pendingQuote = {
    id: 441,
    guildId: selectedGuildId ?? 1,
    guildName: selectedGuild.name,
    userId: 4,
    discordId: "900000000003",
    author: "radu",
    content: "approval queues are just democracy with suspense",
    insertedAtUtc: now.toISOString(),
    approved: false,
    removed: false,
    score: 3,
    scoreVotes: 2,
    pendingApprovals: 1,
    lastVoteAtUtc: now.toISOString(),
  };
  const quoteTimeline = derivedPoints.map((point, index) => ({
    dateUtc: point.dateUtc,
    created: index % 5 === 0 ? 3 : index % 3 === 0 ? 2 : 1,
    approved: index % 4 === 0 ? 2 : 1,
    pending: index % 7 === 0 ? 1 : 0,
    removed: index % 11 === 0 ? 1 : 0,
    score: Math.round(12 + Math.sin(index / 2.4) * 8 + (index % 4) * 3),
    scoreVotes: 4 + (index % 6),
    approvalVotes: index % 4,
  }));
  const quoteScoreTrend = quoteTimeline.reduce<typeof quoteTimeline>((rows, point) => {
    const previous = rows.at(-1);
    rows.push({
      ...point,
      created: point.scoreVotes,
      approved: 0,
      pending: 0,
      removed: 0,
      score: (previous?.score ?? 0) + point.score,
      scoreVotes: (previous?.scoreVotes ?? 0) + point.scoreVotes,
      approvalVotes: 0,
    });
    return rows;
  }, []);
  const quoteApprovalRequest = {
    id: 72,
    quoteId: pendingQuote.id,
    guildId: pendingQuote.guildId,
    guildName: pendingQuote.guildName,
    type: "Add",
    status: "Pending",
    currentApprovals: 1,
    requiredApprovals: 3,
    completionPercent: 33.3,
    insertedAtUtc: pendingQuote.insertedAtUtc,
    completedAtUtc: null,
    expiresAtUtc: new Date(now.getTime() + 1000 * 60 * 60 * 24 * 2).toISOString(),
    expired: false,
    quoteContent: pendingQuote.content,
    author: pendingQuote.author,
  };
  const quoteInsights = {
    total: 129,
    approved: 118,
    pending: 7,
    removed: 4,
    approvalRequests: 34,
    pendingApprovalRequests: 5,
    expiredApprovalRequests: 2,
    completedApprovalRequests: 27,
    approvalCompletionRate: 79.4,
    averageApprovalTimeHours: 8.6,
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
    creationTimeline: quoteTimeline,
    scoreTrend: quoteScoreTrend,
    approvalFunnel: [
      { label: "Created", value: 129 },
      { label: "Approved", value: 118 },
      { label: "Pending", value: 7 },
      { label: "Removed", value: 4 },
      { label: "Approval requests", value: 34 },
      { label: "Completed", value: 27 },
      { label: "Expired", value: 2 },
    ],
    approvalTimeHistogram: [
      { label: "<1h", count: 4 },
      { label: "1-6h", count: 11 },
      { label: "6-24h", count: 9 },
      { label: "1-3d", count: 3 },
      { label: "3d+", count: 1 },
    ],
    scoreHistogram: [
      { label: "< 0", count: 3 },
      { label: "0", count: 11 },
      { label: "1-5", count: 46 },
      { label: "6-15", count: 55 },
      { label: "16+", count: 14 },
    ],
    approvalActivityCalendar: calendarActivity.map((cell, index) => ({
      ...cell,
      messages: index % 4,
      xp: index % 4,
      activeUsers: index % 3,
    })),
    serverSummaries: guilds.map((guild, index) => ({
      guildId: guild.id,
      discordId: guild.discordId,
      name: guild.name,
      total: guild.approvedQuotes + 8 - index * 2,
      approved: guild.approvedQuotes,
      pending: 5 - index,
      removed: 3 + index,
      approvalRequests: 18 - index * 4,
      pendingApprovalRequests: 3 - index,
      totalScore: 640 - index * 210,
      scoreVotes: 180 - index * 45,
      usesGlobalQuotes: index === 0,
      approvalChannelConfigured: true,
      setupHealth: index === 0 ? "Healthy" : "Weak",
    })),
    globalVsServerUsage: [
      { label: "Global-enabled servers", value: 1 },
      { label: "Server-only servers", value: 1 },
      { label: "Quotes in global-enabled servers", value: 87 },
      { label: "Quotes in server-only servers", value: 42 },
    ],
    setupSummaries: guilds.map((guild, index) => ({
      guildId: guild.id,
      discordId: guild.discordId,
      name: guild.name,
      usesGlobalQuotes: index === 0,
      approvalChannelConfigured: true,
      addRequiredApprovals: index === 0 ? 3 : 1,
      removeRequiredApprovals: index === 0 ? 3 : 1,
      health: index === 0 ? "Healthy" : "Weak",
      issue: index === 0 ? "Global quotes enabled" : "Approval threshold is low",
    })),
    highestScoringQuotes: quotes.map((quote, index) => ({
      rank: index + 1,
      id: quote.id,
      guildId: quote.guildId,
      guildName: guilds.find((guild) => guild.id === quote.guildId)?.name ?? selectedGuild.name,
      userId: quote.userId,
      author: quote.author,
      content: quote.content,
      insertedAtUtc: quote.insertedAtUtc,
      approved: quote.approved,
      removed: quote.removed,
      score: quote.score,
      positiveVotes: 8 - index,
      negativeVotes: index,
      totalVotes: 8 + index,
      controversyScore: index * 3 + 4,
    })),
    lowestScoringQuotes: [
      {
        rank: 1,
        id: 366,
        guildId: selectedGuildId ?? 1,
        guildName: selectedGuild.name,
        userId: 5,
        author: "alex",
        content: "this quote did not survive committee",
        insertedAtUtc: "2026-05-19T12:20:00Z",
        approved: true,
        removed: false,
        score: -4,
        positiveVotes: 1,
        negativeVotes: 5,
        totalVotes: 6,
        controversyScore: 8,
      },
    ],
    mostControversialQuotes: [
      {
        rank: 1,
        id: 390,
        guildId: selectedGuildId ?? 1,
        guildName: selectedGuild.name,
        userId: 2,
        author: "ana",
        content: "the market command is roleplay until someone loses 400 credits",
        insertedAtUtc: "2026-05-22T14:20:00Z",
        approved: true,
        removed: false,
        score: 1,
        positiveVotes: 6,
        negativeVotes: 5,
        totalVotes: 11,
        controversyScore: 21,
      },
    ],
    mostRemovedQuotes: [
      {
        rank: 1,
        id: 312,
        guildId: selectedGuildId ?? 1,
        guildName: selectedGuild.name,
        userId: 7,
        author: "old-name",
        content: "removed quote sample",
        insertedAtUtc: "2026-05-12T10:00:00Z",
        approved: true,
        removed: true,
        score: -2,
        positiveVotes: 2,
        negativeVotes: 4,
        totalVotes: 6,
        controversyScore: 10,
      },
    ],
    quoteOfTheDayCandidates: quotes.slice(0, 2).map((quote, index) => ({
      rank: index + 1,
      period: "Day",
      id: quote.id,
      guildId: quote.guildId,
      guildName: selectedGuild.name,
      author: quote.author,
      content: quote.content,
      score: quote.score,
      votes: 8 - index,
      insertedAtUtc: quote.insertedAtUtc,
    })),
    quoteOfTheWeekCandidates: quotes.map((quote, index) => ({
      rank: index + 1,
      period: "Week",
      id: quote.id,
      guildId: quote.guildId,
      guildName: selectedGuild.name,
      author: quote.author,
      content: quote.content,
      score: quote.score,
      votes: 9 - index,
      insertedAtUtc: quote.insertedAtUtc,
    })),
    quoteOfTheMonthCandidates: quotes.map((quote, index) => ({
      rank: index + 1,
      period: "Month",
      id: quote.id,
      guildId: quote.guildId,
      guildName: selectedGuild.name,
      author: quote.author,
      content: quote.content,
      score: quote.score,
      votes: 12 - index,
      insertedAtUtc: quote.insertedAtUtc,
    })),
    topVoters: [
      { rank: 1, userId: "900000000001", username: "ana", votes: 42, positiveVotes: 35, negativeVotes: 7, score: 28, lastVotedAtUtc: now.toISOString() },
      { rank: 2, userId: "900000000000", username: "vyc", votes: 37, positiveVotes: 29, negativeVotes: 8, score: 21, lastVotedAtUtc: now.toISOString() },
    ],
    approvalVoters: [
      { rank: 1, userId: "1", username: "vyc", votes: 18, positiveVotes: 18, negativeVotes: 0, score: 18, lastVotedAtUtc: now.toISOString() },
      { rank: 2, userId: "2", username: "ana", votes: 14, positiveVotes: 14, negativeVotes: 0, score: 14, lastVotedAtUtc: now.toISOString() },
    ],
    quoteList: [
      ...quotes.map((quote) => ({
        id: quote.id,
        guildId: quote.guildId,
        guildName: guilds.find((guild) => guild.id === quote.guildId)?.name ?? selectedGuild.name,
        userId: quote.userId,
        discordId: String(900000000000 + quote.userId),
        author: quote.author,
        content: quote.content,
        insertedAtUtc: quote.insertedAtUtc,
        approved: quote.approved,
        removed: quote.removed,
        score: quote.score,
        scoreVotes: 8,
        pendingApprovals: 0,
        lastVoteAtUtc: now.toISOString(),
      })),
      pendingQuote,
    ],
    approvalRequestsList: [quoteApprovalRequest],
    pendingApprovalQueue: [quoteApprovalRequest],
    expiredApprovalQueue: [
      {
        ...quoteApprovalRequest,
        id: 68,
        quoteId: 399,
        status: "Expired",
        currentApprovals: 0,
        completionPercent: 0,
        insertedAtUtc: "2026-05-14T10:00:00Z",
        expiresAtUtc: "2026-05-19T10:00:00Z",
        expired: true,
        quoteContent: "expired pending quote sample",
        author: "mira",
      },
    ],
    removedQuoteList: [
      {
        id: 312,
        guildId: selectedGuildId ?? 1,
        guildName: selectedGuild.name,
        userId: 7,
        discordId: "900000000007",
        author: "old-name",
        content: "removed quote sample",
        insertedAtUtc: "2026-05-12T10:00:00Z",
        approved: true,
        removed: true,
        score: -2,
        scoreVotes: 6,
        pendingApprovals: 0,
        lastVoteAtUtc: "2026-05-12T12:00:00Z",
      },
    ],
    recentPending: [
      {
        id: pendingQuote.id,
        guildId: pendingQuote.guildId,
        userId: pendingQuote.userId,
        author: pendingQuote.author,
        content: pendingQuote.content,
        insertedAtUtc: pendingQuote.insertedAtUtc,
        approved: pendingQuote.approved,
        removed: pendingQuote.removed,
        score: pendingQuote.score,
      },
    ],
  };
  const demoWealthLeaders = xpItems.map((item, index) => ({
    rank: index + 1,
    userId: item.userId,
    discordId: item.discordId,
    username: item.username,
    balance: 22400 - index * 1700,
    stockPortfolioValue: 15100 - index * 930,
    netWorth: 37500 - index * 2630,
  }));
  const moneySupplyTrend = economyFlow.reduce<Array<{
    dateUtc: string;
    totalMoneySupply: number;
    cashBalance: number;
    ubiPool: number;
    slotsVault: number;
    inflow: number;
    outflow: number;
  }>>((rows, point, index) => {
    const previousCash = rows.at(-1)?.cashBalance ?? 172000;
    const cashBalance = previousCash + point.net;
    rows.push({
      dateUtc: point.dateUtc,
      totalMoneySupply: cashBalance + 18420 + 126700,
      cashBalance,
      ubiPool: 18420 + index * 18,
      slotsVault: 126700 + Math.sin(index / 4) * 1200,
      inflow: point.inflow,
      outflow: point.outflow,
    });
    return rows;
  }, []);
  const transactionVolumeTimeline = economyFlow.map((point, index) => ({
    dateUtc: point.dateUtc,
    stockBuy: 900 + index * 24,
    stockSell: 620 + index * 18,
    transfer: 420 + (index % 5) * 90,
    donation: index % 4 === 0 ? 260 : 70,
    slotsWin: 220 + (index % 3) * 140,
    slotsLoss: 130 + (index % 4) * 80,
    robberyWin: index % 6 === 0 ? 140 : 24,
    robberyLoss: index % 7 === 0 ? 110 : 18,
    stockTransfer: index % 5 === 0 ? 300 : 40,
    fees: 18 + index * 2,
    taxes: 12 + index,
  }));
  const economyHeatmap = heatmap.map((cell) => ({
    dayOfWeek: cell.dayOfWeek,
    dayLabel: cell.dayLabel,
    hourUtc: cell.hourUtc,
    transactions: Math.max(0, Math.round(cell.messages / 18)),
    volume: Math.max(0, cell.messages * 3.4),
    activeUsers: cell.activeUsers,
  }));
  const economyActors = xpItems.map((item, index) => ({
    rank: index + 1,
    userId: item.userId,
    discordId: item.discordId,
    username: item.username,
    amount: 4200 - index * 520,
    count: 18 - index * 2,
    secondaryAmount: 900 - index * 80,
    label: "events",
  }));
  const economyInsights = {
    totalMoneySupply: 329440,
    cashBalance: 184320,
    portfolioValue: 96780,
    netWorth: 281100,
    averageBalance: 3021.64,
    medianBalance: 1420.5,
    activeWallets: 56,
    activeTraders: 18,
    transactionVolume: 74250,
    transactionCount: 812,
    fees: 1240,
    taxesCollected: 430,
    ubiPoolSize: 18420,
    ubiDonations: 5800,
    wealthTaxImpact: 18.43,
    transfersVolume: 12750,
    userToUserTransferVolume: 11140,
    inflows: economyFlow.reduce((sum, point) => sum + point.inflow, 0),
    outflows: economyFlow.reduce((sum, point) => sum + point.outflow, 0),
    robberyWins: 24,
    robberyLosses: 41,
    robberySuccessRate: 36.9,
    slotsWins: 92,
    slotsLosses: 218,
    slotsVaultSize: 126700,
    slotsPayoutRatio: 0.82,
    moneySupplyTrend,
    dailyFlow: economyFlow,
    ubiPoolTrend: moneySupplyTrend.map((point) => ({ dateUtc: point.dateUtc, inflow: 42, outflow: 0, net: point.ubiPool })),
    slotsVaultTrend: moneySupplyTrend.map((point) => ({ dateUtc: point.dateUtc, inflow: 160, outflow: 130, net: point.slotsVault })),
    slotsProfitLoss: economyFlow.map((point, index) => ({ dateUtc: point.dateUtc, inflow: 180 + index * 7, outflow: 120 + index * 4, net: 60 + index * 3 })),
    transactionVolumeTimeline,
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
    cashLeaders: demoWealthLeaders,
    wealthLeaders: demoWealthLeaders,
    balanceDistribution: [
      { label: "<0", count: 1 },
      { label: "0", count: 4 },
      { label: "1-999", count: 22 },
      { label: "1k-10k", count: 28 },
      { label: "10k-100k", count: 6 },
      { label: "100k+", count: 1 },
    ],
    wealthInequality: [
      { label: "Top 1% share", value: 21.4 },
      { label: "Top 5% share", value: 39.8 },
      { label: "Top 10% share", value: 52.2 },
      { label: "Gini estimate", value: 41.5 },
    ],
    topDonors: economyActors,
    biggestRobberies: economyActors.map((actor) => ({ ...actor, label: "Robbery win" })),
    mostRobbedUsers: economyActors.slice(1),
    mostSuccessfulRobbers: economyActors,
    robberyOutcomes: [
      { label: "Wins", value: 24 },
      { label: "Losses", value: 41 },
    ],
    biggestSlotsWins: economyActors.map((actor) => ({ ...actor, amount: actor.amount * 1.6, label: "Slots win" })),
    biggestSlotsLosses: economyActors.map((actor) => ({ ...actor, amount: actor.amount / 5, label: "Slots loss" })),
    slotsOutcomes: [
      { label: "Wins", value: 92 },
      { label: "Losses", value: 218 },
    ],
    economyHeatmap,
  };
  const stockTableItems = [
    { stockId: 12, entityType: "User", entityId: 2, name: "ana", price: 184.42, previousPrice: 167.96, dailyChangePercent: 9.8, holdingValue: 12400 },
    { stockId: 33, entityType: "Channel", entityId: 4, name: "market", price: 142.14, previousPrice: 132.72, dailyChangePercent: 7.1, holdingValue: 8100 },
    { stockId: 6, entityType: "Server", entityId: 1, name: "Morpheus Lab", price: 121.9, previousPrice: 115.44, dailyChangePercent: 5.6, holdingValue: 17400 },
    { stockId: 44, entityType: "User", entityId: 5, name: "alex", price: 88.3, previousPrice: 94.14, dailyChangePercent: -6.2, holdingValue: 2300 },
    { stockId: 51, entityType: "Channel", entityId: 3, name: "quotes", price: 94.72, previousPrice: 98.46, dailyChangePercent: -3.8, holdingValue: 5100 },
  ].map((stock, index) => ({
    rank: index + 1,
    ...stock,
    sharesHeld: 120 - index * 14,
    holders: 18 - index * 2,
    tradeVolume: 9400 - index * 1200,
    tradeCount: 42 - index * 5,
    insertedAtUtc: new Date(now.getTime() - index * 1000 * 60 * 60 * 24 * 5).toISOString(),
  }));
  const stockMovers = stockTableItems.map((stock) => ({
    stockId: stock.stockId,
    entityType: stock.entityType,
    name: stock.name,
    price: stock.price,
    dailyChangePercent: stock.dailyChangePercent,
    holdingValue: stock.holdingValue,
  }));
  const stockInsights = {
    stocks: 86,
    userStocks: 61,
    serverStocks: 7,
    channelStocks: 18,
    marketValue: 96780,
    averagePrice: 118.42,
    averageDailyChangePercent: 2.4,
    buyVolume: 28400,
    sellVolume: 18100,
    stockTransferVolume: 4100,
    winners: stockMovers.filter((stock) => stock.dailyChangePercent >= 0),
    losers: stockMovers.filter((stock) => stock.dailyChangePercent < 0),
    entityTypes: [
      { label: "User", value: 61 },
      { label: "Channel", value: 18 },
      { label: "Server", value: 7 },
    ],
    mostValuableStocks: stockTableItems,
    mostHeldStocks: [...stockTableItems].sort((a, b) => b.sharesHeld - a.sharesHeld),
    mostTradedStocks: [...stockTableItems].sort((a, b) => b.tradeVolume - a.tradeVolume),
    newestStocks: [...stockTableItems].sort((a, b) => Date.parse(b.insertedAtUtc) - Date.parse(a.insertedAtUtc)),
    dailyChangeHistogram: [
      { label: "<= -10%", count: 2 },
      { label: "-10 to -3%", count: 9 },
      { label: "-3 to 0%", count: 18 },
      { label: "0%", count: 7 },
      { label: "0 to 3%", count: 27 },
      { label: "3 to 10%", count: 18 },
      { label: "10%+", count: 5 },
    ],
    priceMovement: stockTableItems.map((stock) => ({ label: stock.name, value: stock.dailyChangePercent })),
    holdingsByUser: demoWealthLeaders.slice(0, 4).map((user, index) => ({
      rank: index + 1,
      userId: user.userId,
      discordId: user.discordId,
      username: user.username,
      portfolioValue: user.stockPortfolioValue,
      shares: 80 - index * 9,
      holdings: 9 - index,
      ownershipPercent: 18.2 - index * 2.7,
    })),
    holdingsTable: stockTableItems.map((stock, index) => ({
      rank: index + 1,
      userId: xpItems[index % xpItems.length].userId,
      discordId: xpItems[index % xpItems.length].discordId,
      username: xpItems[index % xpItems.length].username,
      stockId: stock.stockId,
      stockName: stock.name,
      entityType: stock.entityType,
      shares: stock.sharesHeld / 2,
      price: stock.price,
      value: stock.holdingValue,
      ownershipPercent: 16.4 - index * 2.1,
      unrealizedGain: 1300 - index * 420,
    })),
    tradeVolumeTimeline: economyFlow.map((point, index) => ({
      dateUtc: point.dateUtc,
      buyVolume: 900 + index * 28,
      sellVolume: 620 + index * 18,
      transferVolume: index % 4 === 0 ? 250 : 80,
      trades: 12 + index % 7,
    })),
    buyVsSell: [
      { label: "Buy volume", value: 28400 },
      { label: "Sell volume", value: 18100 },
      { label: "Transfer activity", value: 4100 },
    ],
    ownershipConcentration: [
      { label: "Top holder", value: 17.6 },
      { label: "Top 3 holders", value: 38.4 },
      { label: "Top 10 holders", value: 64.2 },
      { label: "Remaining holders", value: 35.8 },
    ],
    activityToPrice: stockTableItems.map((stock, index) => ({
      stockId: stock.stockId,
      name: stock.name,
      entityType: stock.entityType,
      messages: 2400 - index * 320,
      xp: 18400 - index * 2200,
      price: stock.price,
      dailyChangePercent: stock.dailyChangePercent,
      holdingValue: stock.holdingValue,
    })),
  };

  const data = {
    usingDemoData: true,
    error,
    globalOverview: {
      generatedAtUtc: now.toISOString(),
      days,
      totals: {
        totalServers: guilds.length,
        totalKnownUsers: 61,
        totalTrackedMessages: 267450,
        totalXpGenerated: 1306300,
        latestDayMessages: latestPoint?.messages ?? 0,
        latestDayXpGenerated: latestPoint?.xp ?? 0,
        totalQuotes: 461,
        totalApprovedQuotes: 436,
        pendingQuotes: 7,
        pendingQuoteApprovals: 5,
        totalEconomyBalance: 184320,
        totalEstimatedNetWorth: 281100,
        ubiPoolSize: 18420,
        slotsVaultSize: 126700,
        totalTransactions: 8124,
        totalButtonPresses: 19340,
        activeReminders: 14,
        recentWarningsOrErrors: 19,
      },
      highlights: {
        mostActiveServersToday: guilds.map((guild, index) => ({
          rank: index + 1,
          guildId: guild.id,
          discordId: guild.discordId,
          name: guild.name,
          messages: 820 - index * 210,
          xp: 6400 - index * 1450,
          activeUsers: 28 - index * 8,
          lastActivityAtUtc: now.toISOString(),
        })),
        mostActiveServersThisWeek: guilds.map((guild, index) => ({
          rank: index + 1,
          guildId: guild.id,
          discordId: guild.discordId,
          name: guild.name,
          messages: 6820 - index * 2210,
          xp: 51400 - index * 15450,
          activeUsers: 39 - index * 12,
          lastActivityAtUtc: now.toISOString(),
        })),
        mostActiveServersThisMonth: guilds.map((guild, index) => ({
          rank: index + 1,
          guildId: guild.id,
          discordId: guild.discordId,
          name: guild.name,
          messages: 23840 - index * 10220,
          xp: 184200 - index * 68300,
          activeUsers: 48 - index * 18,
          lastActivityAtUtc: now.toISOString(),
        })),
        mostActiveServersAllTime: guilds.map((guild, index) => ({
          rank: index + 1,
          guildId: guild.id,
          discordId: guild.discordId,
          name: guild.name,
          messages: guild.messages,
          xp: guild.xp,
          activeUsers: guild.trackedUsers,
          lastActivityAtUtc: now.toISOString(),
        })),
        mostActiveServersSelectedWindow: guilds.map((guild, index) => ({
          rank: index + 1,
          guildId: guild.id,
          discordId: guild.discordId,
          name: guild.name,
          messages: Math.round((23840 - index * 10220) * Math.max(0.2, days / 30)),
          xp: Math.round((184200 - index * 68300) * Math.max(0.2, days / 30)),
          activeUsers: 48 - index * 18,
          lastActivityAtUtc: now.toISOString(),
        })),
        biggestXpGainers: xpItems.map((item, index) => ({
          rank: index + 1,
          userId: item.userId,
          discordId: item.discordId,
          username: item.username,
          messages: 2200 - index * 240,
          xp: item.value,
          level: item.level ?? 0,
          lastActivityAtUtc: item.lastActivityAtUtc,
        })),
        richestUsersByBalance: xpItems.map((item, index) => ({
          rank: index + 1,
          userId: item.userId,
          discordId: item.discordId,
          username: item.username,
          balance: 22400 - index * 1700,
          stockPortfolioValue: 15100 - index * 930,
          netWorth: 37500 - index * 2630,
        })),
        richestUsersByNetWorth: xpItems.map((item, index) => ({
          rank: index + 1,
          userId: item.userId,
          discordId: item.discordId,
          username: item.username,
          balance: 20500 - index * 1300,
          stockPortfolioValue: 18200 - index * 810,
          netWorth: 38700 - index * 2110,
        })),
        biggestStockGainers: [
          { stockId: 12, entityType: "User", name: "ana", price: 184.42, dailyChangePercent: 9.8, holdingValue: 12400 },
          { stockId: 33, entityType: "Channel", name: "market", price: 142.14, dailyChangePercent: 7.1, holdingValue: 8100 },
          { stockId: 6, entityType: "Server", name: "Morpheus Lab", price: 121.9, dailyChangePercent: 5.6, holdingValue: 17400 },
        ],
        biggestStockLosers: [
          { stockId: 44, entityType: "User", name: "alex", price: 88.3, dailyChangePercent: -6.2, holdingValue: 2300 },
          { stockId: 51, entityType: "Channel", name: "quotes", price: 94.72, dailyChangePercent: -3.8, holdingValue: 5100 },
        ],
        mostPopularQuotes: quotes.map((quote, index) => ({
          rank: index + 1,
          id: quote.id,
          guildId: quote.guildId,
          userId: quote.userId,
          author: quote.author,
          content: quote.content,
          insertedAtUtc: quote.insertedAtUtc,
          score: quote.score,
        })),
        mostActiveChannels: channelRows.map(([name, discordId, messages, xp, activeUsers], index) => ({
          rank: index + 1,
          discordId,
          name,
          messages,
          xp,
          activeUsers,
          lastActivityAtUtc: now.toISOString(),
        })),
        mostActiveUsers: messageItems.map((item, index) => ({
          rank: index + 1,
          userId: item.userId,
          discordId: item.discordId,
          username: item.username,
          messages: item.value,
          xp: 18400 - index * 1200,
          level: 6 + index,
          lastActivityAtUtc: item.lastActivityAtUtc,
        })),
        recentlyCreatedUsers: xpItems.slice(0, 4).map((item, index) => ({
          id: item.userId,
          discordId: item.discordId,
          name: item.username,
          insertedAtUtc: new Date(now.getTime() - index * 1000 * 60 * 60 * 12).toISOString(),
        })),
        recentlyCreatedServers: guilds.map((guild) => ({
          id: guild.id,
          discordId: guild.discordId,
          name: guild.name,
          insertedAtUtc: guild.insertedAtUtc,
        })),
        recentlyCreatedQuotes: quotes.map((quote) => ({
          id: quote.id,
          guildId: quote.guildId,
          userId: quote.userId,
          author: quote.author,
          content: quote.content,
          approved: quote.approved,
          removed: quote.removed,
          insertedAtUtc: quote.insertedAtUtc,
        })),
        recentlyCreatedStocks: [
          { stockId: 86, entityType: "User", entityId: 5, name: "alex", price: 88.3, dailyChangePercent: -6.2, insertedAtUtc: now.toISOString() },
          { stockId: 85, entityType: "Channel", entityId: 4, name: "market", price: 142.14, dailyChangePercent: 7.1, insertedAtUtc: new Date(now.getTime() - 1000 * 60 * 50).toISOString() },
        ],
      },
      visuals: {
        activity: derivedPoints,
        stackedServerActivity,
        calendarActivity,
        hourByWeekdayActivity: heatmap,
        transactionTypes: [
          { label: "Stock buy", value: 28400 },
          { label: "Stock sell", value: 18100 },
          { label: "Transfer", value: 12750 },
          { label: "Slots win", value: 9200 },
          { label: "Donation", value: 5800 },
        ],
      },
      feeds: {
        recentEconomyEvents: [
          {
            id: 8124,
            type: "Stock buy",
            amount: 1240,
            fee: 12,
            userId: 1,
            user: "vyc",
            targetUserId: null,
            targetUser: null,
            stockId: 33,
            stockName: "market",
            insertedAtUtc: now.toISOString(),
          },
          {
            id: 8123,
            type: "Donation",
            amount: 500,
            fee: 0,
            userId: 2,
            user: "ana",
            targetUserId: null,
            targetUser: null,
            stockId: null,
            stockName: null,
            insertedAtUtc: new Date(now.getTime() - 1000 * 60 * 36).toISOString(),
          },
        ],
        recentBotHealthEvents: [
          {
            id: 920,
            severity: "Warning",
            message: "Approval message update retried for quote 441",
            version: "1.7.0",
            insertedAtUtc: new Date(now.getTime() - 1000 * 60 * 32).toISOString(),
          },
          {
            id: 919,
            severity: "Error",
            message: "Transient Discord gateway timeout recovered",
            version: "1.7.0",
            insertedAtUtc: new Date(now.getTime() - 1000 * 60 * 85).toISOString(),
          },
        ],
      },
    },
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
      metric: "xp" as const,
      days,
      limit: 10,
      items: xpItems,
    },
    messageLeaderboard: {
      guildId: selectedGuildId,
      metric: "messages" as const,
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
      activityAnalytics,
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
      quotes: quoteInsights,
      economy: economyInsights,
      stocks: stockInsights,
      buttonGame: {
        presses: 924,
        score: 48220,
        averageScore: 52.2,
        medianScore: 49,
        highestScoreEver: 418,
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
        topGlobalScores: xpItems.slice(0, 6).map((item, index) => ({
          rank: index + 1,
          pressId: 700 + index,
          userId: item.userId,
          discordId: item.discordId,
          username: item.username,
          guildId: guilds[index % guilds.length].id,
          guildName: guilds[index % guilds.length].name,
          score: 418 - index * 27,
          insertedAtUtc: new Date(now.getTime() - index * 1000 * 60 * 60 * 9).toISOString(),
        })),
        topServerScores: guilds.slice(0, 4).map((guild, index) => ({
          rank: index + 1,
          pressId: 760 + index,
          userId: xpItems[index].userId,
          discordId: xpItems[index].discordId,
          username: xpItems[index].username,
          guildId: guild.id,
          guildName: guild.name,
          score: 370 - index * 35,
          insertedAtUtc: new Date(now.getTime() - index * 1000 * 60 * 60 * 13).toISOString(),
        })),
        topIndividualScores: xpItems.slice(0, 6).map((item, index) => ({
          rank: index + 1,
          pressId: 820 + index,
          userId: item.userId,
          discordId: item.discordId,
          username: item.username,
          guildId: guilds[index % guilds.length].id,
          guildName: guilds[index % guilds.length].name,
          score: 350 - index * 22,
          insertedAtUtc: new Date(now.getTime() - index * 1000 * 60 * 60 * 7).toISOString(),
        })),
        topUsersByTotalScore: xpItems.slice(0, 8).map((item, index) => ({
          rank: index + 1,
          userId: item.userId,
          discordId: item.discordId,
          username: item.username,
          presses: 180 - index * 14,
          score: 9440 - index * 610,
          lastPressAtUtc: new Date(now.getTime() - index * 1000 * 60 * 41).toISOString(),
        })),
        topUsersByPressCount: xpItems.slice(0, 8).map((item, index) => ({
          rank: index + 1,
          userId: item.userId,
          discordId: item.discordId,
          username: item.username,
          presses: 210 - index * 17,
          score: 8300 - index * 420,
          lastPressAtUtc: new Date(now.getTime() - index * 1000 * 60 * 33).toISOString(),
        })),
        scoreDistribution: [
          { label: "0-25", count: 118 },
          { label: "26-50", count: 248 },
          { label: "51-75", count: 196 },
          { label: "76-100", count: 94 },
          { label: "101-200", count: 38 },
          { label: "201+", count: 7 },
        ],
        pressesByServer: guilds.map((guild, index) => ({ label: guild.name, value: 340 - index * 64 })),
        pressesByHour: Array.from({ length: 24 }, (_, hour) => ({ label: `${String(hour).padStart(2, "0")}:00`, value: 12 + ((hour * 7) % 31) })),
        pressesByWeekday: ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"].map((label, index) => ({ label, value: 82 + index * 11 })),
        hourByWeekdayHeatmap: Array.from({ length: 7 }, (_, day) =>
          Array.from({ length: 24 }, (_, hour) => ({
            dayOfWeek: day,
            dayLabel: ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"][day],
            hourUtc: hour,
            messages: (hour + day) % 5 === 0 ? 9 + day : (hour * day) % 4,
            xp: ((hour + 1) * (day + 2)) % 80,
            activeUsers: ((hour + day) % 4) + 1,
          })),
        ).flat(),
        calendarHeatmap: buttonDaily.map((point) => ({
          dateUtc: point.dateUtc,
          messages: point.presses,
          xp: point.score,
          activeUsers: point.activeUsers,
        })),
        longestGaps: Array.from({ length: 5 }, (_, index) => ({
          rank: index + 1,
          startedAtUtc: new Date(now.getTime() - (index + 4) * 1000 * 60 * 60 * 19).toISOString(),
          endedAtUtc: new Date(now.getTime() - (index + 3) * 1000 * 60 * 60 * 19 + index * 1000 * 60 * 23).toISOString(),
          hours: 14.5 - index * 1.7,
          previousScore: 44 + index * 8,
          nextScore: 96 + index * 12,
        })),
        competitiveServers: guilds.map((guild, index) => ({
          rank: index + 1,
          guildId: guild.id,
          guildName: guild.name,
          presses: 340 - index * 51,
          score: 14200 - index * 2100,
          activeUsers: 28 - index * 4,
          averageScore: 51.7 - index * 2.1,
          competitiveScore: 96 - index * 9.4,
          lastPressAtUtc: new Date(now.getTime() - index * 1000 * 60 * 55).toISOString(),
        })),
      },
      operations: {
        reminders: {
          pending: 14,
          overdue: 2,
          dueNext24Hours: 5,
          averageLeadTimeHours: 34.6,
          upcoming: [
            {
              id: 18,
              channelId: "1165553796223602001",
              text: "ship dashboard review",
              user: "vyc",
              server: guilds[0].name,
              createdAtUtc: new Date(now.getTime() - 1000 * 60 * 60 * 4).toISOString(),
              dueDateUtc: new Date(now.getTime() + 1000 * 60 * 80).toISOString(),
              overdue: false,
            },
            {
              id: 19,
              channelId: "1165553796223602004",
              text: "check stock reset",
              user: "ana",
              server: guilds[1].name,
              createdAtUtc: new Date(now.getTime() - 1000 * 60 * 60 * 16).toISOString(),
              dueDateUtc: new Date(now.getTime() + 1000 * 60 * 60 * 7).toISOString(),
              overdue: false,
            },
          ],
          byServer: guilds.map((guild, index) => ({ label: guild.name, value: 8 - index * 2 })),
          byUser: xpItems.slice(0, 6).map((item, index) => ({ label: item.username, value: 7 - index })),
          byChannel: channelRows.slice(0, 6).map((channel, index) => ({ label: channel[0], value: 6 - index })),
          creationTrend: points.map((point, index) => ({
            dateUtc: point.dateUtc,
            created: index % 5 === 0 ? 3 : index % 3,
            due: index % 4 === 0 ? 4 : index % 2,
            overdue: index < points.length - 10 && index % 9 === 0 ? 1 : 0,
            upcoming: index >= points.length - 8 ? 2 : 0,
          })),
          dueTimeline: points.slice(-30).map((point, index) => ({
            dateUtc: point.dateUtc,
            created: index % 4,
            due: index % 6 === 0 ? 5 : index % 3,
            overdue: index < 18 && index % 7 === 0 ? 1 : 0,
            upcoming: index >= 22 ? 2 + (index % 3) : 0,
          })),
          calendar: points.map((point, index) => ({
            dateUtc: point.dateUtc,
            messages: index % 4 === 0 ? 3 : index % 2,
            xp: 0,
            activeUsers: index % 4,
          })),
        },
        moderation: {
          pendingTemporaryBans: 3,
          overdueTemporaryBans: 1,
          completedLast30Days: 11,
          reactionRoleMessages: 9,
          reactionRoleItems: 42,
          pending: [
            {
              id: 8,
              guildId: guilds[0].discordId,
              userId: "882222331111",
              reason: "temporary cooldown",
              insertedAtUtc: new Date(now.getTime() - 1000 * 60 * 60 * 20).toISOString(),
              expiresAtUtc: new Date(now.getTime() + 1000 * 60 * 60 * 3).toISOString(),
              unbannedAtUtc: null,
              status: "Pending",
            },
          ],
          temporaryBanTimeline: points.slice(-30).map((point, index) => ({
            dateUtc: point.dateUtc,
            created: index % 8 === 0 ? 2 : index % 5 === 0 ? 1 : 0,
            completed: index % 7 === 0 ? 1 : 0,
            expiring: index % 6 === 0 ? 1 : 0,
            overdue: index % 17 === 0 ? 1 : 0,
          })),
          banStatus: [
            { label: "Pending", value: 3 },
            { label: "Overdue", value: 1 },
            { label: "Completed 30d", value: 11 },
          ],
          banReasons: [
            { label: "temporary cooldown", value: 5 },
            { label: "spam", value: 3 },
            { label: "No reason", value: 2 },
          ],
          reactionRoleTypes: [
            { label: "Button messages", value: 5 },
            { label: "Emoji messages", value: 4 },
            { label: "Button items", value: 27 },
            { label: "Emoji items", value: 15 },
          ],
          reactionRoleUsage: guilds.map((guild, index) => ({
            guildId: guild.id,
            guildName: guild.name,
            messages: 5 - index,
            items: 22 - index * 4,
            buttonMessages: 3 - Math.min(index, 2),
            emojiMessages: 2,
          })),
          activityRoleDistribution: [
            { label: "TopOnePercent", value: 2 },
            { label: "TopFivePercent", value: 2 },
            { label: "TopTenPercent", value: 1 },
            { label: "TopTwentyPercent", value: 1 },
            { label: "TopThirtyPercent", value: 1 },
          ],
          serverScorecards: guilds.map((guild, index) => ({
            guildId: guild.id,
            guildName: guild.name,
            score: 92 - index * 14,
            risk: index === 0 ? "Strong" : index === 1 ? "Watch" : "Weak",
            passedChecks: 8 - index,
            failedChecks: 1 + index,
            notes: index === 0 ? ["Core setup checks pass."] : ["Quote approval channel", "Pins channel"],
          })),
          incompleteServerSetup: [
            { guildId: guilds[1].id, guildName: guilds[1].name, severity: "Missing", label: "Pins channel", detail: "Pins channel is missing" },
            { guildId: guilds[1].id, guildName: guilds[1].name, severity: "Missing", label: "Activity roles", detail: "Activity roles enabled without configured roles" },
          ],
          riskyConfiguration: [
            { guildId: guilds[1].id, guildName: guilds[1].name, severity: "Risk", label: "Quote add threshold", detail: "Add threshold is weak" },
            { guildId: guilds[1].id, guildName: guilds[1].name, severity: "Risk", label: "Quote approval channel", detail: "Quote approvals have no channel" },
          ],
        },
        logs: {
          total: points.reduce((sum, point) => sum + Math.round(point.messages / 3), 0),
          warnings: 16,
          errors: 3,
          critical: 1,
          latestAtUtc: now.toISOString(),
          severityCounts: [
            { severity: "Info", count: 246 },
            { severity: "Warning", count: 16 },
            { severity: "Error", count: 3 },
            { severity: "Critical", count: 1 },
          ],
          timeline: points.map((point, index) => ({
            dateUtc: point.dateUtc,
            total: Math.round(point.messages / 3),
            warnings: index % 6 === 0 ? 3 : index % 4,
            errors: index % 13 === 0 ? 1 : 0,
          })),
          recent: [
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
          logsByVersion: [
            { label: "1.7.0", value: 221 },
            { label: "1.6.9", value: 44 },
          ],
          commonMessages: [
            { label: "Quartz Job - Stock update completed for # entities", value: 38 },
            { label: "Approval message update retried for quote #", value: 9 },
          ],
          recentIncidents: [
            {
              id: 920,
              severity: "Warning",
              message: "Approval message update retried for quote 441",
              version: "1.7.0",
              insertedAtUtc: new Date(now.getTime() - 1000 * 60 * 32).toISOString(),
            },
          ],
          healthIndicators: [
            { label: "Healthy log share", value: 91.4 },
            { label: "Warning rate", value: 7.1 },
            { label: "Error/critical rate", value: 1.5 },
            { label: "Incident count", value: 4 },
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
      server: selectedGuild
        ? {
            identity: {
              guildId: selectedGuild.id,
              discordId: selectedGuild.discordId,
              name: selectedGuild.name,
              insertedAtUtc: selectedGuild.insertedAtUtc,
            },
            totals: {
              knownUsers: selectedGuild.trackedUsers,
              trackedMessages: selectedGuild.messages,
              totalXp: selectedGuild.xp,
              totalQuotes: 129,
              approvedQuotes: selectedGuild.approvedQuotes,
              pendingQuotes: 7,
              pendingQuoteApprovals: 5,
              removedQuotes: 4,
              activeReminders: 14,
              buttonPresses: 924,
              economyTransactions: 248,
              economyVolume: 74250,
              stockActivity: 22,
              stockMarketValue: 96780,
              lastActivityAtUtc: now.toISOString(),
            },
            configuration: {
              prefix: "m!",
              welcomeChannel: { discordId: "1165553796223602001", name: "general", configured: true },
              pinsChannel: { discordId: "1165553796223602003", name: "quotes", configured: true },
              honeypotChannel: { discordId: "", name: "Not configured", configured: false },
              honeypotMessages: false,
              levelUpMessages: true,
              levelUpMessageChannel: { discordId: "1165553796223602001", name: "general", configured: true },
              levelUpQuoteMessages: true,
              levelUpQuoteChannel: { discordId: "1165553796223602003", name: "quotes", configured: true },
              quoteApprovalChannel: { discordId: "1165553796223602003", name: "quotes", configured: true },
              quoteAddRequiredApprovals: 5,
              quoteRemoveRequiredApprovals: 5,
              globalQuotes: false,
              activityRoles: true,
            },
            health: {
              score: 84,
              label: "Healthy",
              activityScore: 88,
              configurationScore: 89,
              operationsScore: 76,
              engagementScore: 82,
              notes: [
                "Activity is trending upward in the selected window.",
                "5 quote approvals are waiting.",
              ],
            },
            configurationChecklist: [
              { label: "Prefix", passed: true, detail: "m!", severity: "success" },
              { label: "Welcome channel", passed: true, detail: "general", severity: "success" },
              { label: "Pins channel", passed: true, detail: "quotes", severity: "success" },
              { label: "Honeypot channel", passed: true, detail: "Honeypot messages disabled", severity: "success" },
              { label: "Level-up channel", passed: true, detail: "general", severity: "success" },
              { label: "Quote approval channel", passed: true, detail: "quotes", severity: "success" },
              { label: "Activity roles", passed: true, detail: "5 configured role records", severity: "success" },
            ],
            topUsersByAverageMessageLength: xpItems.slice(0, 5).map((item, index) => ({
              rank: index + 1,
              userId: item.userId,
              discordId: item.discordId,
              username: item.username,
              value: 74 - index * 4.4,
              unit: "chars",
              lastActivityAtUtc: item.lastActivityAtUtc,
            })),
            fastestRisingUsers: xpItems.slice(0, 4).map((item, index) => ({
              rank: index + 1,
              userId: item.userId,
              discordId: item.discordId,
              username: item.username,
              previousMessages: 120 - index * 8,
              recentMessages: 220 - index * 12,
              delta: 100 - index * 4,
              deltaPercent: 83.3 - index * 8,
            })),
            droppingUsers: xpItems.slice(1, 4).map((item, index) => ({
              rank: index + 1,
              userId: item.userId,
              discordId: item.discordId,
              username: item.username,
              previousMessages: 260 - index * 30,
              recentMessages: 170 - index * 18,
              delta: -90 + index * 12,
              deltaPercent: -34.6 + index * 5,
            })),
            quietestChannels: channelRows.slice().reverse().map(([name, discordId, messages, xp, activeUsers], index) => ({
              rank: index + 1,
              discordId,
              name,
              messages: Math.round(messages / 8),
              xp: Math.round(xp / 8),
              activeUsers,
              averageMessageLength: 38 + index * 3.1,
              lastActivityAtUtc: now.toISOString(),
            })),
            bestActivityDays: derivedPoints
              .slice()
              .sort((a, b) => b.messages - a.messages)
              .slice(0, 5)
              .map((point, index) => ({
                label: point.dateUtc.slice(0, 10),
                sort: index,
                messages: point.messages,
                xp: point.xp,
                activeUsers: point.activeUsers,
              })),
            worstActivityDays: derivedPoints
              .slice()
              .sort((a, b) => a.messages - b.messages)
              .slice(0, 5)
              .map((point, index) => ({
                label: point.dateUtc.slice(0, 10),
                sort: index,
                messages: point.messages,
                xp: point.xp,
                activeUsers: point.activeUsers,
              })),
            peakHours: [
              { label: "20:00 UTC", sort: 20, messages: 842, xp: 6720, activeUsers: 28 },
              { label: "21:00 UTC", sort: 21, messages: 788, xp: 6150, activeUsers: 25 },
              { label: "19:00 UTC", sort: 19, messages: 731, xp: 5880, activeUsers: 23 },
            ],
            peakWeekdays: [
              { label: "Fri", sort: 5, messages: 3842, xp: 29740, activeUsers: 39 },
              { label: "Sat", sort: 6, messages: 3420, xp: 25840, activeUsers: 34 },
              { label: "Thu", sort: 4, messages: 3188, xp: 24400, activeUsers: 33 },
            ],
            activityRoleDistribution: [
              { label: "Top 1%", value: 1 },
              { label: "1-5%", value: 2 },
              { label: "5-10%", value: 3 },
              { label: "10-20%", value: 4 },
              { label: "20-30%", value: 4 },
              { label: "Other tracked users", value: Math.max(0, selectedGuild.trackedUsers - 14) },
            ],
            userRankMovement: xpItems.slice(0, 5).map((item, index) => ({
              userId: item.userId,
              discordId: item.discordId,
              username: item.username,
              previousRank: index + 2,
              currentRank: index + 1,
              rankChange: 1,
              previousXp: Math.round(item.value * 0.42),
              currentXp: Math.round(item.value * 0.58),
            })),
            channelHeatmap: buildDemoChannelHeatmap(days, now, channelRows),
          }
        : null,
      userProfile: demoUserProfile,
      filterOptions: {
        users: userOptions,
        channels: channelOptions,
      },
    },
  };

  return {
    ...data,
    filterOptions: data.insights.filterOptions,
    drilldown: {
      activity: data.activity,
      xpLeaderboard: data.xpLeaderboard,
      messageLeaderboard: data.messageLeaderboard,
      quotes: data.quotes,
      insights: data.insights,
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

function buildDemoActivityAnalytics(
  days: number,
  now: Date,
  guilds: DashboardGuildSummary[],
  channelRows: ReadonlyArray<readonly [string, string, number, number, number]>,
  xpItems: Array<{ rank: number; userId: number; discordId: string; username: string; value: number; level: number | null; lastActivityAtUtc: string }>,
  derivedPoints: DashboardActivityDerivedPoint[],
  heatmap: DashboardHeatmapCell[],
): DashboardActivityAnalytics {
  const totalMessages = derivedPoints.reduce((sum, point) => sum + point.messages, 0);
  const totalXp = derivedPoints.reduce((sum, point) => sum + point.xp, 0);
  const dailyActiveUsers = derivedPoints.map((point, index) => ({
    label: point.dateUtc.slice(0, 10),
    sort: index,
    messages: point.messages,
    xp: point.xp,
    activeUsers: point.activeUsers,
  }));
  const weeklyActiveUsers = chunkBuckets(dailyActiveUsers, 7, "week");
  const monthlyActiveUsers = chunkBuckets(dailyActiveUsers, 30, "month");
  const messageShareByUser = xpItems.map((item, index) => {
    const messages = Math.max(80, Math.round(totalMessages * (0.26 - index * 0.035)));
    const xp = item.value;
    return {
      id: String(item.userId),
      label: item.username,
      kind: "user",
      messages,
      xp,
      sharePercent: Math.round((messages / Math.max(1, totalMessages)) * 1000) / 10,
    };
  });
  const messageShareByChannel = channelRows.map(([name, discordId, messages, xp]) => ({
    id: discordId,
    label: name,
    kind: "channel",
    messages,
    xp,
    sharePercent: Math.round((messages / Math.max(1, channelRows.reduce((sum, row) => sum + row[2], 0))) * 1000) / 10,
  }));
  const messageShareByServer = guilds.map((guild) => ({
    id: String(guild.id),
    label: guild.name,
    kind: "server",
    messages: Math.round(guild.messages / 12),
    xp: Math.round(guild.xp / 12),
    sharePercent: Math.round((guild.messages / Math.max(1, guilds.reduce((sum, row) => sum + row.messages, 0))) * 1000) / 10,
  }));
  const comparisonSeries = [
    { key: "selected-window", label: "Selected window", kind: "time-range", points: derivedPoints },
    {
      key: "previous-window",
      label: "Previous window",
      kind: "time-range",
      points: derivedPoints.map((point) => ({
        ...point,
        messages: Math.round(point.messages * 0.82),
        xp: Math.round(point.xp * 0.78),
        activeUsers: Math.max(1, Math.round(point.activeUsers * 0.85)),
        cumulativeMessages: Math.round(point.cumulativeMessages * 0.82),
        cumulativeXp: Math.round(point.cumulativeXp * 0.78),
      })),
    },
    ...xpItems.slice(0, 3).map((item, index) => ({
      key: `user-${item.userId}`,
      label: item.username,
      kind: "user",
      points: derivedPoints.map((point) => ({
        ...point,
        messages: Math.max(0, Math.round(point.messages * (0.28 - index * 0.045))),
        xp: Math.max(0, Math.round(point.xp * (0.31 - index * 0.05))),
        activeUsers: point.messages > 0 ? 1 : 0,
        cumulativeMessages: Math.max(0, Math.round(point.cumulativeMessages * (0.28 - index * 0.045))),
        cumulativeXp: Math.max(0, Math.round(point.cumulativeXp * (0.31 - index * 0.05))),
      })),
    })),
    ...channelRows.slice(0, 3).map(([name, discordId], index) => ({
      key: `channel-${discordId}`,
      label: name,
      kind: "channel",
      points: derivedPoints.map((point) => ({
        ...point,
        messages: Math.max(0, Math.round(point.messages * (0.34 - index * 0.07))),
        xp: Math.max(0, Math.round(point.xp * (0.32 - index * 0.06))),
        activeUsers: Math.max(1, Math.round(point.activeUsers * (0.44 - index * 0.08))),
        cumulativeMessages: Math.max(0, Math.round(point.cumulativeMessages * (0.34 - index * 0.07))),
        cumulativeXp: Math.max(0, Math.round(point.cumulativeXp * (0.32 - index * 0.06))),
      })),
    })),
  ];
  const channelHourHeatmap = channelRows.slice(0, 4).flatMap(([name, discordId, messages, xp, activeUsers], channelIndex) =>
    Array.from({ length: 24 }, (_, hour) => {
      const hourBoost = hour >= 18 && hour <= 23 ? 1.8 : hour >= 8 && hour <= 14 ? 1.1 : 0.45;
      const count = Math.round((messages / 45) * hourBoost * (1 + Math.sin((hour + channelIndex) / 4) * 0.35));
      return {
        channelId: discordId,
        channelName: name,
        hourUtc: hour,
        messages: Math.max(0, count),
        xp: Math.max(0, Math.round((xp / 45) * hourBoost)),
        activeUsers: Math.min(activeUsers, Math.max(0, Math.round(count / 9))),
      };
    }),
  );
  const serverDayHeatmap = guilds.flatMap((guild, guildIndex) =>
    derivedPoints.map((point, dayIndex) => ({
      dateUtc: point.dateUtc,
      guildId: guild.id,
      guildName: guild.name,
      messages: Math.max(0, Math.round(point.messages * (guildIndex === 0 ? 0.68 : 0.32) * (0.8 + Math.sin(dayIndex / 4) * 0.18))),
      xp: Math.max(0, Math.round(point.xp * (guildIndex === 0 ? 0.7 : 0.3))),
      activeUsers: Math.max(1, Math.round(point.activeUsers * (guildIndex === 0 ? 0.72 : 0.38))),
    })),
  );
  const scatter = [
    ...messageShareByUser,
    ...messageShareByChannel,
    ...messageShareByServer,
  ].map((row, index) => ({
    id: row.id,
    label: row.label,
    kind: row.kind,
    messages: row.messages,
    xp: row.xp,
    averageMessageLength: 42 + index * 4.8,
    xpPerMessage: Math.round((row.xp / Math.max(1, row.messages)) * 100) / 100,
  }));
  const userContributionPareto = messageShareByUser
    .slice()
    .sort((left, right) => right.messages - left.messages)
    .reduce<Array<{ id: string; label: string; value: number; sharePercent: number; cumulativePercent: number }>>((rows, row) => {
      const previous = rows.at(-1)?.cumulativePercent ?? 0;
      rows.push({
        id: row.id,
        label: row.label,
        value: row.messages,
        sharePercent: row.sharePercent,
        cumulativePercent: Math.min(100, Math.round((previous + row.sharePercent) * 10) / 10),
      });
      return rows;
    }, []);
  const leaderboardRows = xpItems.map((item, index) => ({
    rank: index + 1,
    entityId: String(item.userId),
    label: item.username,
    entityType: "user",
    value: item.value,
    unit: "XP",
    messages: 2400 - index * 270,
    xp: item.value,
    level: item.level,
    averageMessageLength: 68 - index * 3.4,
    xpPerMessage: 9.2 - index * 0.4,
    lastActivityAtUtc: now.toISOString(),
    deltaPercent: null,
  }));

  return {
    dailyActiveUsers,
    weeklyActiveUsers,
    monthlyActiveUsers,
    bestActivityDays: dailyActiveUsers.slice().sort((a, b) => b.messages - a.messages).slice(0, 7),
    worstActivityDays: dailyActiveUsers.slice().sort((a, b) => a.messages - b.messages).slice(0, 7),
    peakHours: heatmap.slice().sort((a, b) => b.messages - a.messages).slice(0, 6).map((cell) => ({
      label: `${cell.hourUtc.toString().padStart(2, "0")}:00 UTC`,
      sort: cell.hourUtc,
      messages: cell.messages,
      xp: cell.xp,
      activeUsers: cell.activeUsers,
    })),
    peakWeekdays: ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"].map((label, day) => {
      const dayCells = heatmap.filter((cell) => cell.dayOfWeek === day);
      return {
        label,
        sort: day,
        messages: dayCells.reduce((sum, cell) => sum + cell.messages, 0),
        xp: dayCells.reduce((sum, cell) => sum + cell.xp, 0),
        activeUsers: Math.max(...dayCells.map((cell) => cell.activeUsers), 0),
      };
    }).sort((left, right) => right.messages - left.messages),
    activityStreaks: {
      currentStreakDays: Math.min(days, 9),
      currentStreakStartUtc: derivedPoints.at(-Math.min(days, 9))?.dateUtc ?? null,
      currentStreakEndUtc: derivedPoints.at(-1)?.dateUtc ?? null,
      longestStreakDays: Math.min(days, 18),
      longestStreakStartUtc: derivedPoints.at(-Math.min(days, 18))?.dateUtc ?? null,
      longestStreakEndUtc: derivedPoints.at(-1)?.dateUtc ?? null,
      activeDays: derivedPoints.filter((point) => point.messages > 0).length,
      quietDays: derivedPoints.filter((point) => point.messages === 0).length,
    },
    comparisonSeries,
    xpByUser: messageShareByUser.slice().sort((left, right) => right.xp - left.xp),
    xpByChannel: messageShareByChannel.slice().sort((left, right) => right.xp - left.xp),
    xpByServer: messageShareByServer.slice().sort((left, right) => right.xp - left.xp),
    messageShareByUser,
    messageShareByChannel,
    messageShareByServer,
    messageLengthHistogram: [
      { label: "0-20", count: 420 },
      { label: "21-60", count: 1740 },
      { label: "61-120", count: 1320 },
      { label: "121-240", count: 620 },
      { label: "241+", count: 104 },
    ],
    messageLengthTrend: derivedPoints.map((point, index) => ({
      dateUtc: point.dateUtc,
      averageMessageLength: Math.round((48 + Math.sin(index / 3) * 10 + index % 4) * 10) / 10,
      movingAverage: Math.round((52 + Math.sin(index / 5) * 8) * 10) / 10,
      messages: point.messages,
    })),
    messageLengthBoxPlots: [
      { label: "All messages", kind: "all", minimum: 2, q1: 31, median: 58, q3: 104, maximum: 340, average: 67.4, count: totalMessages },
      ...channelRows.slice(0, 4).map(([name], index) => ({
        label: name,
        kind: "channel",
        minimum: 2 + index,
        q1: 28 + index * 4,
        median: 52 + index * 5,
        q3: 96 + index * 8,
        maximum: 280 + index * 24,
        average: 58 + index * 6.2,
        count: 800 - index * 120,
      })),
    ],
    messageCountVsXp: scatter,
    averageLengthVsXp: scatter,
    channelHourHeatmap,
    serverDayHeatmap,
    channelDayHeatmap: buildDemoChannelHeatmap(days, now, channelRows),
    userContributionPareto,
    leaderboards: [
      { key: "global-xp", title: "Global XP", metric: "xp", unit: "XP", items: leaderboardRows },
      { key: "server-xp", title: "Server XP", metric: "xp", unit: "XP", items: leaderboardRows.slice().reverse().map((row, index) => ({ ...row, rank: index + 1 })) },
      { key: "recent-xp", title: `XP in past ${days} days`, metric: "xp", unit: "XP", items: leaderboardRows.map((row) => ({ ...row, value: Math.round(row.value / 4), xp: Math.round(row.xp / 4) })) },
      { key: "global-messages", title: "Global messages", metric: "messages", unit: "messages", items: leaderboardRows.map((row) => ({ ...row, value: row.messages })) },
      { key: "server-messages", title: "Server messages", metric: "messages", unit: "messages", items: leaderboardRows.map((row) => ({ ...row, value: Math.round(row.messages * 0.72) })) },
      { key: "recent-messages", title: `Messages in past ${days} days`, metric: "messages", unit: "messages", items: leaderboardRows.map((row) => ({ ...row, value: Math.round(row.messages / 3) })) },
      { key: "average-message-length", title: "Average message length", metric: "average-message-length", unit: "chars", items: leaderboardRows.map((row) => ({ ...row, value: row.averageMessageLength })) },
      { key: "weighted-average-message-length", title: "Weighted global average message length", metric: "average-message-length", unit: "chars", items: leaderboardRows.map((row) => ({ ...row, value: row.averageMessageLength - 2 })) },
      { key: "fastest-level-gainers", title: "Fastest level gainers", metric: "levels", unit: "levels", items: leaderboardRows.map((row, index) => ({ ...row, value: Math.max(0, 3 - index) })) },
      { key: "most-consistent-users", title: "Most consistent users", metric: "consistency", unit: "%", items: leaderboardRows.map((row, index) => ({ ...row, value: 94 - index * 6, averageMessageLength: 15 - index })) },
      {
        key: "most-active-channels",
        title: "Most active channels",
        metric: "messages",
        unit: "messages",
        items: messageShareByChannel.map((row, index) => ({
          rank: index + 1,
          entityId: row.id,
          label: row.label,
          entityType: "channel",
          value: row.messages,
          unit: "messages",
          messages: row.messages,
          xp: row.xp,
          level: null,
          averageMessageLength: 48 + index * 6,
          xpPerMessage: Math.round((row.xp / Math.max(1, row.messages)) * 100) / 100,
          lastActivityAtUtc: now.toISOString(),
          deltaPercent: null,
        })),
      },
      {
        key: "most-active-servers",
        title: "Most active servers",
        metric: "messages",
        unit: "messages",
        items: messageShareByServer.map((row, index) => ({
          rank: index + 1,
          entityId: row.id,
          label: row.label,
          entityType: "server",
          value: row.messages,
          unit: "messages",
          messages: row.messages,
          xp: row.xp,
          level: null,
          averageMessageLength: 54 + index * 4,
          xpPerMessage: Math.round((row.xp / Math.max(1, row.messages)) * 100) / 100,
          lastActivityAtUtc: now.toISOString(),
          deltaPercent: null,
        })),
      },
      { key: "rising-users", title: "Rising users", metric: "rising", unit: "messages", items: leaderboardRows.slice(0, 4).map((row, index) => ({ ...row, value: 120 - index * 18, deltaPercent: 64 - index * 9 })) },
      { key: "falling-users", title: "Falling users", metric: "falling", unit: "messages", items: leaderboardRows.slice(1, 5).map((row, index) => ({ ...row, rank: index + 1, value: -42 - index * 12, deltaPercent: -18 - index * 7 })) },
    ],
    rankMovement: leaderboardRows.slice(0, 5).map((row, index) => ({
      userId: Number(row.entityId),
      discordId: String(900000000000 + index),
      username: row.label,
      previousRank: index + 2,
      currentRank: index + 1,
      rankChange: 1,
      previousXp: Math.round(row.xp * 0.42),
      currentXp: Math.round(row.xp * 0.58),
    })),
  };
}

function chunkBuckets(
  buckets: Array<{ label: string; sort: number; messages: number; xp: number; activeUsers: number }>,
  size: number,
  label: string,
) {
  const chunks: Array<{ label: string; sort: number; messages: number; xp: number; activeUsers: number }> = [];
  for (let index = 0; index < buckets.length; index += size) {
    const chunk = buckets.slice(index, index + size);
    chunks.push({
      label: `${chunk[0]?.label ?? label} ${label}`,
      sort: index,
      messages: chunk.reduce((sum, row) => sum + row.messages, 0),
      xp: chunk.reduce((sum, row) => sum + row.xp, 0),
      activeUsers: Math.max(...chunk.map((row) => row.activeUsers), 0),
    });
  }
  return chunks;
}

function buildStackedServerActivity(days: number, now: Date, guilds: DashboardGuildSummary[]) {
  return Array.from({ length: days }, (_, dayIndex) => {
    const date = new Date(now);
    date.setUTCHours(0, 0, 0, 0);
    date.setUTCDate(date.getUTCDate() - (days - dayIndex - 1));

    return guilds.map((guild, guildIndex) => ({
      dateUtc: date.toISOString(),
      guildId: guild.id,
      guildName: guild.name,
      messages: Math.round(80 + (Math.sin((dayIndex + guildIndex) / 2.8) + 1.2) * (55 - guildIndex * 12)),
    }));
  }).flat();
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

function buildDemoChannelHeatmap(
  days: number,
  now: Date,
  channelRows: ReadonlyArray<readonly [string, string, number, number, number]>,
) {
  return Array.from({ length: days }, (_, dayIndex) => {
    const date = new Date(now);
    date.setUTCHours(0, 0, 0, 0);
    date.setUTCDate(date.getUTCDate() - (days - dayIndex - 1));

    return channelRows.map(([channelName, channelId, messages, xp], channelIndex) => {
      const wave = Math.sin((dayIndex + channelIndex) / 2.4) + 1.2;
      const dailyMessages = Math.max(0, Math.round((messages / Math.max(1, days)) * (0.35 + wave / 2)));

      return {
        dateUtc: date.toISOString(),
        channelId,
        channelName,
        messages: dailyMessages,
        xp: Math.round((xp / Math.max(1, days)) * (0.35 + wave / 2)),
      };
    });
  }).flat();
}

function buildDemoUserProfile(
  userId: number,
  xpItems: Array<{ rank: number; userId: number; discordId: string; username: string; value: number; level: number | null; lastActivityAtUtc: string }>,
  guilds: DashboardGuildSummary[],
  channelRows: ReadonlyArray<readonly [string, string, number, number, number]>,
  derivedPoints: DashboardActivityDerivedPoint[],
  heatmap: DashboardHeatmapCell[],
  economyFlow: DashboardEconomyFlowPoint[],
  buttonDaily: DashboardButtonGamePoint[],
  now: Date,
) {
  const user = xpItems.find((item) => item.userId === userId) ?? xpItems[0];
  const totalMessages = derivedPoints.reduce((sum, point) => sum + point.messages, 0);
  const totalXp = user.value + 42000;
  const serverLevels = guilds.map((guild, index) => ({
    guildId: guild.id,
    discordId: guild.discordId,
    name: guild.name,
    level: Math.max(1, (user.level ?? 7) - index),
    totalXp: Math.max(1200, Math.round(totalXp * (index === 0 ? 0.68 : 0.32))),
    messages: Math.max(120, Math.round(totalMessages * (index === 0 ? 0.72 : 0.28))),
    averageMessageLength: 58 + index * 7.2,
    messageLengthMovingAverage: 61 + index * 4.4,
    rank: index + 2,
    rankPopulation: guild.trackedUsers,
    lastActivityAtUtc: now.toISOString(),
  }));
  const holdings = [
    { stockId: 12, entityType: "User", name: "ana", shares: 14, price: 184.42, totalInvested: 2140, dailyChangePercent: 9.8 },
    { stockId: 33, entityType: "Channel", name: "market", shares: 9, price: 142.14, totalInvested: 990, dailyChangePercent: 7.1 },
    { stockId: 6, entityType: "Server", name: "Morpheus Lab", shares: 6, price: 121.9, totalInvested: 760, dailyChangePercent: 5.6 },
  ].map((holding) => {
    const value = Math.round(holding.shares * holding.price);
    return {
      ...holding,
      value,
      unrealizedGain: value - holding.totalInvested,
    };
  });
  const transactions = [
    { id: 8124, type: "Stock buy", amount: 1240, fee: 12, direction: "Outgoing", stockId: 33, stockName: "market", insertedAtUtc: now.toISOString() },
    { id: 8108, type: "Slots win", amount: 920, fee: 0, direction: "Incoming", stockId: null, stockName: null, insertedAtUtc: new Date(now.getTime() - 1000 * 60 * 90).toISOString() },
    { id: 8077, type: "Robbery loss", amount: 310, fee: 0, direction: "Outgoing", stockId: null, stockName: null, insertedAtUtc: new Date(now.getTime() - 1000 * 60 * 220).toISOString() },
    { id: 8041, type: "Donation", amount: 500, fee: 0, direction: "Outgoing", stockId: null, stockName: null, insertedAtUtc: new Date(now.getTime() - 1000 * 60 * 360).toISOString() },
  ].map((transaction) => ({
    ...transaction,
    counterpartyUserId: null,
    counterpartyUsername: null,
  }));
  const reminders = [
    {
      id: 18,
      guildName: guilds[0].name,
      channelId: channelRows[0][1],
      text: "ship dashboard review",
      createdAtUtc: new Date(now.getTime() - 1000 * 60 * 60 * 4).toISOString(),
      dueDateUtc: new Date(now.getTime() + 1000 * 60 * 80).toISOString(),
      overdue: false,
    },
    {
      id: 19,
      guildName: guilds[1]?.name ?? guilds[0].name,
      channelId: channelRows[3][1],
      text: "check stock reset",
      createdAtUtc: new Date(now.getTime() - 1000 * 60 * 60 * 24).toISOString(),
      dueDateUtc: new Date(now.getTime() + 1000 * 60 * 60 * 7).toISOString(),
      overdue: false,
    },
  ];

  return {
    identity: {
      userId: user.userId,
      discordId: user.discordId,
      username: user.username,
      insertedAtUtc: new Date(now.getTime() - 1000 * 60 * 60 * 24 * 180).toISOString(),
      levelUpMessages: true,
      levelUpQuotes: true,
    },
    totals: {
      totalXp,
      globalLevel: user.level ?? 7,
      totalMessages,
      averageMessageLength: 62.4,
      messageLengthMovingAverage: 66.1,
      xpPerMessage: 8.9,
      knownServers: serverLevels.length,
      knownChannels: channelRows.length,
      mostActiveServer: serverLevels[0]?.name ?? "No server activity",
      mostActiveChannel: channelRows[0][0],
      quoteContributions: 32,
      quoteScoresReceived: 184,
      quoteVotesGiven: 91,
      economyBalance: 20500,
      portfolioValue: holdings.reduce((sum, holding) => sum + holding.value, 0),
      estimatedNetWorth: 20500 + holdings.reduce((sum, holding) => sum + holding.value, 0),
      buttonScore: buttonDaily.reduce((sum, point) => sum + point.score, 0),
      lastActivityAtUtc: now.toISOString(),
    },
    activity: {
      messages: totalMessages,
      xp: derivedPoints.reduce((sum, point) => sum + point.xp, 0),
      activeUsers: 1,
      activeChannels: channelRows.length,
      averageMessageLength: 62.4,
      messagesPerActiveUser: totalMessages,
      xpPerMessage: 8.9,
      peakHourUtc: 20,
      trendPercent: 12.8,
      points: derivedPoints,
    },
    serverLevels,
    bestActivityDays: derivedPoints.slice().sort((a, b) => b.messages - a.messages).slice(0, 7).map((point, index) => ({
      label: point.dateUtc.slice(0, 10),
      sort: index,
      messages: point.messages,
      xp: point.xp,
      activeUsers: 1,
    })),
    worstActivityDays: derivedPoints.slice().sort((a, b) => a.messages - b.messages).slice(0, 7).map((point, index) => ({
      label: point.dateUtc.slice(0, 10),
      sort: index,
      messages: point.messages,
      xp: point.xp,
      activeUsers: 1,
    })),
    activityStreaks: {
      currentStreakDays: Math.min(9, derivedPoints.length),
      currentStreakStartUtc: derivedPoints.at(-9)?.dateUtc ?? derivedPoints[0]?.dateUtc ?? null,
      currentStreakEndUtc: derivedPoints.at(-1)?.dateUtc ?? null,
      longestStreakDays: Math.min(14, derivedPoints.length),
      longestStreakStartUtc: derivedPoints.at(-14)?.dateUtc ?? derivedPoints[0]?.dateUtc ?? null,
      longestStreakEndUtc: derivedPoints.at(-1)?.dateUtc ?? null,
      activeDays: derivedPoints.filter((point) => point.messages > 0).length,
      quietDays: derivedPoints.filter((point) => point.messages === 0).length,
    },
    serverContribution: serverLevels.map((level) => ({
      id: String(level.guildId),
      label: level.name,
      messages: level.messages,
      xp: level.totalXp,
      percent: Math.round((level.messages / Math.max(1, totalMessages)) * 1000) / 10,
    })),
    channelContribution: channelRows.map(([name, discordId, messages, xp]) => ({
      id: discordId,
      label: name,
      messages: Math.round(messages / 5),
      xp: Math.round(xp / 5),
      percent: Math.round((messages / channelRows.reduce((sum, row) => sum + row[2], 0)) * 1000) / 10,
    })),
    globalRank: {
      guildId: null,
      scope: "Global",
      rank: 4,
      population: 61,
      xp: totalXp,
      messages: totalMessages,
    },
    serverRanks: serverLevels.map((level) => ({
      guildId: level.guildId,
      scope: level.name,
      rank: level.rank,
      population: level.rankPopulation,
      xp: level.totalXp,
      messages: level.messages,
    })),
    rankMovement: derivedPoints.map((point, index) => ({
      dateUtc: point.dateUtc,
      globalRank: Math.max(1, 8 - Math.floor(index / 5)),
      serverRank: Math.max(1, 5 - Math.floor(index / 8)),
      userXp: point.cumulativeXp,
      leadingXp: point.cumulativeXp + 8000 + index * 90,
    })),
    quotePerformance: {
      contributions: 14,
      approved: 12,
      pending: 1,
      removed: 1,
      scoreReceived: 86,
      votesGiven: 28,
      averageScore: 6.1,
      scoreByServer: serverLevels.map((level, index) => ({ label: level.name, value: 64 - index * 22 })),
      recentQuotes: [
        {
          id: 404,
          guildId: guilds[0].id,
          userId: user.userId,
          author: user.username,
          content: "the bot has become sentient but only for charts",
          insertedAtUtc: "2026-05-25T13:00:00Z",
          approved: true,
          removed: false,
          score: 22,
        },
      ],
    },
    economyPerformance: {
      balance: 20500,
      portfolioValue: holdings.reduce((sum, holding) => sum + holding.value, 0),
      netWorth: 20500 + holdings.reduce((sum, holding) => sum + holding.value, 0),
      transactionVolume: transactions.reduce((sum, transaction) => sum + transaction.amount, 0),
      feesPaid: transactions.reduce((sum, transaction) => sum + transaction.fee, 0),
      realizedGains: 1840,
      unrealizedGains: holdings.reduce((sum, holding) => sum + holding.unrealizedGain, 0),
      trades: 8,
      donations: { count: 1, total: 500, recent: transactions.filter((transaction) => transaction.type === "Donation") },
      robbery: { wins: 2, losses: 1, won: 740, lost: 310, net: 430, recent: transactions.filter((transaction) => transaction.type.startsWith("Robbery")) },
      slots: { wins: 3, losses: 2, won: 1820, lost: 760, net: 1060, recent: transactions.filter((transaction) => transaction.type.startsWith("Slots")) },
      dailyFlow: economyFlow,
      transactionTypes: [
        { label: "Stock buy", value: 2840 },
        { label: "Slots win", value: 1820 },
        { label: "Robbery win", value: 740 },
        { label: "Donation", value: 500 },
      ],
      recentTransactions: transactions,
      tradingHistory: transactions.filter((transaction) => transaction.type.startsWith("Stock")),
    },
    stockHoldings: holdings,
    buttonGame: {
      presses: buttonDaily.reduce((sum, point) => sum + point.presses, 0),
      score: buttonDaily.reduce((sum, point) => sum + point.score, 0),
      averageScore: 52.2,
      bestScore: 410,
      lastPressAtUtc: now.toISOString(),
      daily: buttonDaily,
      scoreTimeline: buttonDaily.map((point) => ({
        insertedAtUtc: point.dateUtc,
        score: point.score,
        cumulativeScore: point.cumulativeScore,
        serverName: guilds[0].name,
      })),
    },
    reminders,
    messageLengthHistogram: [
      { label: "0-20", count: 18 },
      { label: "21-60", count: 144 },
      { label: "61-120", count: 98 },
      { label: "121-240", count: 31 },
      { label: "241+", count: 9 },
    ],
    messageLengthTrend: derivedPoints.map((point, index) => ({
      dateUtc: point.dateUtc,
      averageMessageLength: 48 + Math.round((Math.sin(index / 3) + 1.2) * 12),
      movingAverage: 52 + Math.round((Math.sin(index / 4) + 1.2) * 8),
      messages: point.messages,
    })),
    levelProgression: derivedPoints.map((point, index) => ({
      dateUtc: point.dateUtc,
      totalXp: Math.max(0, totalXp - derivedPoints.at(-1)!.cumulativeXp + point.cumulativeXp),
      level: Math.max(1, (user.level ?? 7) - Math.max(0, Math.floor((derivedPoints.length - index) / 18))),
    })),
    activityCalendar: derivedPoints.map((point) => ({
      dateUtc: point.dateUtc,
      messages: point.messages,
      xp: point.xp,
      activeUsers: 1,
    })),
    hourByWeekdayHeatmap: heatmap,
  };
}
