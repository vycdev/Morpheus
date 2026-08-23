namespace Morpheus.MCP;

public sealed record McpGuildInfo(
    int Id,
    string Name,
    int TrackedUsers,
    long Messages,
    long Xp,
    int ApprovedQuotes);

public sealed record McpActivityOverview(
    long TotalMessages,
    long TotalXp,
    int ActiveUsersLast30Days,
    long MessagesLast30Days,
    long XpLast30Days,
    int TotalServers,
    int TotalKnownUsers);

public sealed record McpQuotePage(
    int Page,
    int TotalPages,
    int Total,
    IReadOnlyList<McpQuoteItem> Items);

public sealed record McpQuoteItem(
    int Id,
    int GuildId,
    string Author,
    string Content,
    DateTime InsertedAtUtc,
    long Score);

public sealed record McpQuoteDetail(
    int Id,
    int GuildId,
    string Content,
    DateTime InsertedAtUtc,
    long TotalScore,
    string Author);

public sealed record McpLeaderboardEntry(
    int Rank,
    string Username,
    long Value,
    int? Level);
