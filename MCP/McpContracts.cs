using System.Text.Json.Serialization;

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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Description,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Url,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Author,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? AuthorIconUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Footer,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? FooterIconUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? ImageUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? ThumbnailUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] uint? Color,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTimeOffset? Timestamp,
    IReadOnlyList<McpCapturedEmbedField> Fields);

public sealed record McpCapturedFile(
    string Filename,
    long Size,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Sha256,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? ContentType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Base64Data,
    bool Truncated);

public sealed record McpCapturedOutput(
    int Sequence,
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Content,
    IReadOnlyList<McpCapturedEmbed> Embeds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] McpCapturedFile? File,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Detail);

public sealed record McpCommandExecutionResult(
    string RequestId,
    string Mode,
    string Status,
    bool Success,
    bool Executed,
    bool SideEffectsMayHaveOccurred,
    bool IdempotentReplay,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] McpCommandCapability? Command,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? ErrorReason,
    IReadOnlyList<McpCapturedOutput> Outputs,
    long ElapsedMilliseconds);
