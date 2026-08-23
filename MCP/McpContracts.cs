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

public sealed record McpCommandAttachment(
    string Filename,
    string Url,
    long Size,
    string? ContentType = null,
    string? Description = null);

public sealed record McpCommandInvocation(
    string Command,
    string UserId,
    string ChannelId,
    string? GuildId = null,
    string? SourceMessageId = null,
    string? MessageContent = null,
    string? ReplyToMessageId = null,
    IReadOnlyList<McpCommandAttachment>? Attachments = null,
    string Mode = "validate",
    string? IdempotencyKey = null,
    string ResponseMode = "capture",
    string? Locale = null,
    string? TimeZoneId = null);

public sealed record McpCapturedEmbedField(string Name, string Value, bool Inline);

public sealed record McpCapturedEmbed(
    string? Title,
    string? Description,
    string? Url,
    string? Author,
    string? AuthorIconUrl,
    string? Footer,
    string? FooterIconUrl,
    string? ImageUrl,
    string? ThumbnailUrl,
    uint? Color,
    DateTimeOffset? Timestamp,
    IReadOnlyList<McpCapturedEmbedField> Fields);

public sealed record McpCapturedFile(
    string Filename,
    long Size,
    string? Sha256,
    string? ContentType,
    string? Base64Data,
    bool Truncated);

public sealed record McpCapturedOutput(
    int Sequence,
    string Kind,
    string? Content,
    IReadOnlyList<McpCapturedEmbed> Embeds,
    McpCapturedFile? File,
    string? Detail);

public sealed record McpCommandExecutionResult(
    string RequestId,
    string Mode,
    string Status,
    bool Success,
    bool Executed,
    bool SideEffectsMayHaveOccurred,
    bool IdempotentReplay,
    McpCommandCapability? Command,
    string? Error,
    string? ErrorReason,
    IReadOnlyList<McpCapturedOutput> Outputs,
    long ElapsedMilliseconds);
