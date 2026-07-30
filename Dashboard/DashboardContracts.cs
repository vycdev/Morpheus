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

public sealed record DashboardCalendarActivityCell(
    DateTime DateUtc,
    int Messages,
    long Xp,
    int ActiveUsers);

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
    int UserId,
    string GuildName,
    string Content,
    DateTime InsertedAtUtc,
    bool Approved,
    bool Removed,
    int TotalScore,
    string Author,
    IReadOnlyList<DashboardQuoteVoteItem> Voters,
    IReadOnlyList<DashboardQuoteApprovalRequestItem> ApprovalRequests);

public sealed record DashboardQuoteStatusSlice(
    string Status,
    int Count);

public sealed record DashboardQuoteAuthorSummary(
    int UserId,
    string DiscordId,
    string Username,
    int Quotes,
    int Score);

public sealed record DashboardQuoteTimelinePoint(
    DateTime DateUtc,
    int Created,
    int Approved,
    int Pending,
    int Removed,
    int Score,
    int ScoreVotes,
    int ApprovalVotes);

public sealed record DashboardQuoteServerSummary(
    int GuildId,
    string DiscordId,
    string Name,
    int Total,
    int Approved,
    int Pending,
    int Removed,
    int ApprovalRequests,
    int PendingApprovalRequests,
    int TotalScore,
    int ScoreVotes,
    bool UsesGlobalQuotes,
    bool ApprovalChannelConfigured,
    string SetupHealth);

public sealed record DashboardQuoteSetupSummary(
    int GuildId,
    string DiscordId,
    string Name,
    bool UsesGlobalQuotes,
    bool ApprovalChannelConfigured,
    int AddRequiredApprovals,
    int RemoveRequiredApprovals,
    string Health,
    string Issue);

public sealed record DashboardQuoteRankedItem(
    int Rank,
    int Id,
    int GuildId,
    string GuildName,
    int UserId,
    string Author,
    string Content,
    DateTime InsertedAtUtc,
    bool Approved,
    bool Removed,
    int Score,
    int PositiveVotes,
    int NegativeVotes,
    int TotalVotes,
    int ControversyScore);

public sealed record DashboardQuoteCandidate(
    int Rank,
    string Period,
    int Id,
    int GuildId,
    string GuildName,
    string Author,
    string Content,
    int Score,
    int Votes,
    DateTime InsertedAtUtc);

public sealed record DashboardQuoteVoteItem(
    int Rank,
    int UserId,
    string DiscordId,
    string Username,
    int Votes,
    int PositiveVotes,
    int NegativeVotes,
    int Score,
    DateTime? LastVotedAtUtc);

public sealed record DashboardQuoteManagementItem(
    int Id,
    int GuildId,
    string GuildName,
    int UserId,
    string DiscordId,
    string Author,
    string Content,
    DateTime InsertedAtUtc,
    bool Approved,
    bool Removed,
    int Score,
    int ScoreVotes,
    int PendingApprovals,
    DateTime? LastVoteAtUtc);

public sealed record DashboardQuoteApprovalRequestItem(
    int Id,
    int QuoteId,
    int GuildId,
    string GuildName,
    string Type,
    string Status,
    int CurrentApprovals,
    int RequiredApprovals,
    double CompletionPercent,
    DateTime InsertedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime ExpiresAtUtc,
    bool Expired,
    string QuoteContent,
    string Author);

public sealed record DashboardHistogramBucket(
    string Label,
    int Count);

public sealed record DashboardCategoryValue(
    string Label,
    decimal Value);

internal sealed record DashboardLeaderboardRow(
    int UserId,
    long Value,
    DateTime? LastActivityAtUtc);
