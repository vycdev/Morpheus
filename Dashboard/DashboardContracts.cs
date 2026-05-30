namespace Morpheus.Dashboard;

public sealed record DashboardOverviewResponse(
    DateTime GeneratedAtUtc,
    DateTime StartedAtUtc,
    long UptimeSeconds,
    DashboardSystemStats System,
    DashboardActivityStats Activity,
    DashboardQuoteStats Quotes,
    DashboardEconomyStats Economy,
    DashboardLogStats Logs);

public sealed record DashboardSystemStats(
    int Guilds,
    int Users,
    int Stocks);

public sealed record DashboardActivityStats(
    long TotalMessages,
    long TotalXp,
    int ActiveUsersLast30Days,
    long MessagesLast30Days,
    long XpLast30Days,
    DateTime? LastActivityAtUtc);

public sealed record DashboardQuoteStats(
    int Approved,
    int Pending,
    int Removed,
    int TotalScores);

public sealed record DashboardEconomyStats(
    decimal TotalBalance,
    decimal StockPortfolioValue);

public sealed record DashboardLogStats(
    long Total,
    int Last24Hours,
    DateTime? LastLogAtUtc);

public sealed record DashboardGuildSummary(
    int Id,
    string DiscordId,
    string Name,
    DateTime InsertedAtUtc,
    int TrackedUsers,
    long Messages,
    long Xp,
    int ApprovedQuotes);

public sealed record DashboardActivitySeriesResponse(
    int? GuildId,
    int Days,
    IReadOnlyList<DashboardActivityPoint> Points);

public sealed record DashboardActivityPoint(
    DateTime DateUtc,
    int Messages,
    long Xp,
    int ActiveUsers,
    double AverageMessageLength);

public sealed record DashboardLeaderboardResponse(
    int? GuildId,
    string Metric,
    int? Days,
    int Limit,
    IReadOnlyList<DashboardLeaderboardItem> Items);

public sealed record DashboardLeaderboardItem(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    long Value,
    int? Level,
    DateTime? LastActivityAtUtc);

public sealed record DashboardQuotePageResponse(
    int Page,
    int TotalPages,
    int Total,
    IReadOnlyList<DashboardQuoteItem> Items);

public sealed record DashboardQuoteItem(
    int Id,
    int GuildId,
    int UserId,
    string Author,
    string Content,
    DateTime InsertedAtUtc,
    bool Approved,
    bool Removed,
    int Score);

public sealed record DashboardQuoteDetailsResponse(
    int Id,
    int GuildId,
    string Content,
    DateTime InsertedAtUtc,
    bool Approved,
    bool Removed,
    int TotalScore,
    string Author);

internal sealed record DashboardLeaderboardRow(
    int UserId,
    long Value,
    DateTime? LastActivityAtUtc);
