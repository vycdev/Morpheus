using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Morpheus.MCP;

/// <summary>
/// Extension methods for registering and mapping the MCP API endpoints.
/// Follows the same pattern as the Dashboard API.
/// </summary>
public static class McpApiExtensions
{
    private const string CorsPolicyName = "McpCors";

    private static readonly IReadOnlyList<McpToolDefinition> ToolDefinitions =
    [
        new McpToolDefinition(
            "get_user_stats",
            "Get detailed statistics for a user including balance, XP, messages, level, quotes, and activity.",
            [
                new McpToolParameter("userId", "integer", "Internal user ID (optional if discordId is provided)", false),
                new McpToolParameter("discordId", "string", "Discord user ID (optional if userId is provided)", false),
            ]),
        new McpToolDefinition(
            "get_guild_info",
            "Get information about a Discord server/guild including settings, activity stats, and quote count.",
            [
                new McpToolParameter("guildId", "integer", "Internal guild ID (optional if discordId is provided)", false),
                new McpToolParameter("discordId", "string", "Discord guild ID (optional if guildId is provided)", false),
            ]),
        new McpToolDefinition(
            "get_economy_summary",
            "Get overall economy summary including total balances, UBI pool size, slots vault, and stock market info.",
            []),
        new McpToolDefinition(
            "get_activity_overview",
            "Get global activity overview including total messages, XP, active users, and server counts.",
            []),
        new McpToolDefinition(
            "get_guilds",
            "List all Discord servers the bot is connected to with their activity stats.",
            []),
        new McpToolDefinition(
            "get_users",
            "Get a paginated list of all known users.",
            [
                new McpToolParameter("page", "integer", "Page number (default: 1)", false, 1),
                new McpToolParameter("limit", "integer", "Results per page (default: 20, max: 100)", false, 20),
            ]),
        new McpToolDefinition(
            "get_quotes",
            "Get a paginated list of quotes with optional filtering by guild, sort order, and approval status.",
            [
                new McpToolParameter("page", "integer", "Page number (default: 1)", false, 1),
                new McpToolParameter("sort", "string", "Sort order: newest, oldest, or score (default: newest)", false, "newest"),
                new McpToolParameter("approvedOnly", "boolean", "Only show approved quotes (default: true)", false, true),
                new McpToolParameter("guildId", "integer", "Filter by guild ID (optional)", false),
            ]),
        new McpToolDefinition(
            "get_quote_by_id",
            "Get detailed information about a specific quote by its ID.",
            [
                new McpToolParameter("quoteId", "integer", "Quote ID", true),
            ]),
        new McpToolDefinition(
            "get_recent_logs",
            "Get recent bot log entries, optionally filtered by severity level.",
            [
                new McpToolParameter("limit", "integer", "Number of entries (default: 20, max: 100)", false, 20),
                new McpToolParameter("severity", "string", "Filter by severity: Info, Warning, Error, Verbose, Debug (optional)", false),
            ]),
        new McpToolDefinition(
            "get_stock_summary",
            "Get stock market summary including total stocks, top gainers, and top losers.",
            [
                new McpToolParameter("limit", "integer", "Number of gainers/losers to return (default: 5)", false, 5),
            ]),
        new McpToolDefinition(
            "get_leaderboard",
            "Get activity leaderboard by XP or messages, optionally filtered by guild and time period.",
            [
                new McpToolParameter("metric", "string", "Metric: xp or messages (default: xp)", false, "xp"),
                new McpToolParameter("guildId", "integer", "Filter by guild ID (optional)", false),
                new McpToolParameter("days", "integer", "Lookback period in days (default: 30)", false, 30),
                new McpToolParameter("limit", "integer", "Number of entries (default: 10, max: 50)", false, 10),
            ]),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Registers MCP API services in the DI container.
    /// </summary>
    public static IServiceCollection AddMcpApi(
        this IServiceCollection services,
        McpApiOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<McpService>();

        services.ConfigureHttpJsonOptions(jsonOptions =>
        {
            jsonOptions.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        services.AddCors(corsOptions =>
        {
            corsOptions.AddPolicy(CorsPolicyName, policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }

    /// <summary>
    /// Maps MCP API endpoints to the application.
    /// </summary>
    public static WebApplication MapMcpApi(this WebApplication app)
    {
        app.MapGet("/api/mcp", () => Results.Ok(new McpServerInfo(
            "Morpheus MCP Server",
            "1.0.0",
            "/api/mcp/health",
            "/api/mcp/tools",
            "/api/mcp/call/{toolName}",
            [.. ToolDefinitions.Select(t => t.Name)]
        )));

        app.MapGet("/api/mcp/health", (McpApiOptions options) => Results.Ok(new
        {
            status = "ok",
            service = "Morpheus MCP Server",
            version = "1.0.0",
            startedAtUtc = Utilities.Env.StartTime,
            authEnabled = !string.IsNullOrWhiteSpace(options.ApiKey)
        }));

        RouteGroupBuilder api = app
            .MapGroup("/api/mcp")
            .RequireCors(CorsPolicyName);

        // List available tools (MCP protocol discovery)
        api.MapGet("/tools", () => Results.Ok(new
        {
            tools = ToolDefinitions
        }));

        // Call a specific tool
        api.MapPost("/call/{toolName}", async (
            string toolName,
            McpToolCallRequest? request,
            McpService mcpService,
            CancellationToken cancellationToken) =>
        {
            Dictionary<string, object?> parameters = request?.Params ?? [];

            try
            {
                object? result = toolName.ToLowerInvariant() switch
                {
                    "get_user_stats" => await mcpService.GetUserStatsAsync(
                        GetIntParam(parameters, "userId"),
                        GetULongParam(parameters, "discordId"),
                        cancellationToken),

                    "get_guild_info" => await mcpService.GetGuildInfoAsync(
                        GetIntParam(parameters, "guildId"),
                        GetULongParam(parameters, "discordId"),
                        cancellationToken),

                    "get_economy_summary" => await mcpService.GetEconomySummaryAsync(cancellationToken),

                    "get_activity_overview" => await mcpService.GetActivityOverviewAsync(cancellationToken),

                    "get_guilds" => await mcpService.GetGuildsAsync(cancellationToken),

                    "get_users" => await mcpService.GetUsersAsync(
                        GetIntParam(parameters, "page") ?? 1,
                        GetIntParam(parameters, "limit") ?? 20,
                        cancellationToken),

                    "get_quotes" => await mcpService.GetQuotesAsync(
                        GetIntParam(parameters, "page") ?? 1,
                        GetStringParam(parameters, "sort") ?? "newest",
                        GetBoolParam(parameters, "approvedOnly") ?? true,
                        GetIntParam(parameters, "guildId"),
                        cancellationToken),

                    "get_quote_by_id" => await mcpService.GetQuoteByIdAsync(
                        GetIntParam(parameters, "quoteId") ?? throw new ArgumentException("quoteId is required"),
                        cancellationToken),

                    "get_recent_logs" => await mcpService.GetRecentLogsAsync(
                        GetIntParam(parameters, "limit") ?? 20,
                        GetStringParam(parameters, "severity"),
                        cancellationToken),

                    "get_stock_summary" => await mcpService.GetStockSummaryAsync(
                        GetIntParam(parameters, "limit") ?? 5,
                        cancellationToken),

                    "get_leaderboard" => await mcpService.GetLeaderboardAsync(
                        GetStringParam(parameters, "metric") ?? "xp",
                        GetIntParam(parameters, "guildId"),
                        GetIntParam(parameters, "days") ?? 30,
                        GetIntParam(parameters, "limit") ?? 10,
                        cancellationToken),

                    _ => null
                };

                if (result is null && toolDefinitionsLut.TryGetValue(toolName.ToLowerInvariant(), out _))
                {
                    // Known tool but returned null (e.g. entity not found)
                    return Results.Ok(new McpToolResponse(false, null, "Not found or no parameters provided."));
                }

                if (result is null)
                {
                    return Results.NotFound(new McpToolResponse(false, null, $"Unknown tool: {toolName}"));
                }

                return Results.Ok(new McpToolResponse(true, result));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new McpToolResponse(false, null, ex.Message));
            }
            catch (Exception ex)
            {
                return Results.Ok(new McpToolResponse(false, null, $"Internal error: {ex.Message}"));
            }
        });

        return app;
    }

    private static readonly Dictionary<string, bool> toolDefinitionsLut = ToolDefinitions
        .ToDictionary(t => t.Name, _ => true);

    // ── Parameter Helpers ──

    private static int? GetIntParam(Dictionary<string, object?> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out object? value) || value is null)
            return null;

        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out int intVal))
                return intVal;
            if (je.ValueKind == JsonValueKind.String && int.TryParse(je.GetString(), out int parsed))
                return parsed;
            return null;
        }

        if (value is int i) return i;
        if (value is long l) return (int)l;
        if (value is string s && int.TryParse(s, out int parsedStr)) return parsedStr;
        return null;
    }

    private static ulong? GetULongParam(Dictionary<string, object?> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out object? value) || value is null)
            return null;

        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number && je.TryGetUInt64(out ulong ulVal))
                return ulVal;
            if (je.ValueKind == JsonValueKind.String && ulong.TryParse(je.GetString(), out ulong parsed))
                return parsed;
            return null;
        }

        if (value is ulong ul) return ul;
        if (value is string s && ulong.TryParse(s, out ulong parsedStr)) return parsedStr;
        return null;
    }

    private static string? GetStringParam(Dictionary<string, object?> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out object? value) || value is null)
            return null;

        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString(),
                JsonValueKind.Number => je.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        return value?.ToString();
    }

    private static bool? GetBoolParam(Dictionary<string, object?> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out object? value) || value is null)
            return null;

        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.True) return true;
            if (je.ValueKind == JsonValueKind.False) return false;
            if (je.ValueKind == JsonValueKind.String && bool.TryParse(je.GetString(), out bool parsed))
                return parsed;
            return null;
        }

        if (value is bool b) return b;
        if (value is string s && bool.TryParse(s, out bool parsedStr)) return parsedStr;
        return null;
    }
}