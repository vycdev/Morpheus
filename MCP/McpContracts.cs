using System.Text.Json.Serialization;

namespace Morpheus.MCP;

/// <summary>
/// MCP tool definition returned by the /tools endpoint.
/// Describes a callable tool, its parameters, and purpose.
/// </summary>
public sealed record McpToolDefinition(
    string Name,
    string Description,
    IReadOnlyList<McpToolParameter> Parameters);

/// <summary>
/// Describes a single parameter for an MCP tool.
/// </summary>
public sealed record McpToolParameter(
    string Name,
    string Type,
    string Description,
    bool Required = false,
    object? Default = null);

/// <summary>
/// MCP server info response.
/// </summary>
public sealed record McpServerInfo(
    string Service,
    string Version,
    string Health,
    string Tools,
    string Call,
    string[] AvailableTools);

/// <summary>
/// Request body for calling an MCP tool.
/// </summary>
public sealed record McpToolCallRequest(
    [property: JsonPropertyName("params")]
    Dictionary<string, object?>? Params);

/// <summary>
/// Successful MCP tool response.
/// </summary>
public sealed record McpToolResponse(
    bool Success,
    object? Data,
    string? Error = null);

/// <summary>
/// User stats response.
/// </summary>
public sealed record McpUserStats(
    int Id,
    ulong DiscordId,
    string Username,
    DateTime CreatedAtUtc,
    decimal Balance,
    int TotalMessages,
    long TotalXp,
    int? Level,
    int QuoteCount,
    int QuoteScore,
    int ButtonScore,
    DateTime? LastActivityAtUtc);

/// <summary>
/// Guild info response.
/// </summary>
public sealed record McpGuildInfo(
    int Id,
    ulong DiscordId,
    string Name,
    DateTime CreatedAtUtc,
    string Prefix,
    int TrackedUsers,
    long Messages,
    long Xp,
    int ApprovedQuotes,
    bool UseGlobalQuotes,
    bool WelcomeMessages,
    bool UseActivityRoles);

/// <summary>
/// Economy summary response.
/// </summary>
public sealed record McpEconomySummary(
    int TotalUsers,
    decimal TotalBalance,
    decimal AverageBalance,
    decimal UbiPoolSize,
    decimal SlotsVaultSize,
    int TotalStocks,
    decimal TotalStockPortfolioValue);

/// <summary>
/// Activity overview response.
/// </summary>
public sealed record McpActivityOverview(
    long TotalMessages,
    long TotalXp,
    int ActiveUsersLast30Days,
    long MessagesLast30Days,
    long XpLast30Days,
    int TotalServers,
    int TotalKnownUsers,
    DateTime? LastActivityAtUtc);

/// <summary>
/// Server list item.
/// </summary>
public sealed record McpServerItem(
    int Id,
    ulong DiscordId,
    string Name,
    DateTime CreatedAtUtc,
    int TrackedUsers,
    long Messages,
    long Xp,
    int ApprovedQuotes);

/// <summary>
/// User list item.
/// </summary>
public sealed record McpUserItem(
    int Id,
    ulong DiscordId,
    string Username,
    DateTime CreatedAtUtc,
    decimal Balance,
    long Messages,
    long Xp,
    int? Level);

/// <summary>
/// Leaderboard entry.
/// </summary>
public sealed record McpLeaderboardEntry(
    int Rank,
    int UserId,
    ulong DiscordId,
    string Username,
    long Value,
    int? Level);

/// <summary>
/// Page of quotes.
/// </summary>
public sealed record McpQuotePage(
    int Page,
    int TotalPages,
    int Total,
    IReadOnlyList<McpQuoteItem> Items);

/// <summary>
/// Single quote item.
/// </summary>
public sealed record McpQuoteItem(
    int Id,
    int GuildId,
    int UserId,
    string Author,
    string Content,
    DateTime InsertedAtUtc,
    bool Approved,
    bool Removed,
    int Score);

/// <summary>
/// Quote detail.
/// </summary>
public sealed record McpQuoteDetail(
    int Id,
    int GuildId,
    string Content,
    DateTime InsertedAtUtc,
    bool Approved,
    bool Removed,
    int TotalScore,
    string Author);

/// <summary>
/// Moderation log entry.
/// </summary>
public sealed record McpModerationEntry(
    long Id,
    string Severity,
    string Message,
    DateTime InsertedAtUtc);

/// <summary>
/// Stock market summary.
/// </summary>
public sealed record McpStockSummary(
    int TotalStocks,
    IReadOnlyList<McpStockItem> TopGainers,
    IReadOnlyList<McpStockItem> TopLosers);

/// <summary>
/// Stock market item.
/// </summary>
public sealed record McpStockItem(
    int StockId,
    string EntityType,
    int EntityId,
    string Name,
    decimal Price,
    decimal DailyChangePercent);