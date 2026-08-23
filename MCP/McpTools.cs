using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Morpheus.MCP;

[McpServerToolType]
public sealed class McpTools(
    McpService service,
    McpCommandCatalog commandCatalog,
    McpCommandExecutionService commandExecution)
{
    [McpServerTool(
        Name = "list_commands",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("List every non-hidden Morpheus text command and alias directly from the live Discord command registry, including parameters, preconditions, and possible effects.")]
    public McpCommandManifest ListCommands() => commandCatalog.GetManifest();

    [McpServerTool(
        Name = "describe_command",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Describe one Morpheus text command by any registered alias.")]
    public McpCommandCapability? DescribeCommand(
        [Description("A command name or alias without the Discord prefix.")] string alias)
    {
        if (string.IsNullOrWhiteSpace(alias) || alias.Length > 100)
            throw new ArgumentException("alias must contain between 1 and 100 characters.", nameof(alias));

        return commandCatalog.FindByAlias(alias);
    }

    [McpServerTool(
        Name = "run_command",
        ReadOnly = false,
        Destructive = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Validate or execute any non-hidden Morpheus text command using a Discord-equivalent context. Identities and permissions are resolved by Morpheus from Discord; supplied ids never grant permissions. Use validate first. Execute mode must be enabled by the server and requires a real source message plus a unique idempotency key because database, Discord, or external side effects may occur.")]
    public Task<McpCommandExecutionResult> RunCommandAsync(
        [Description("The complete command invocation and Discord context. Command text must omit the server prefix. IDs are decimal strings. Execute mode requires sourceMessageId. Validate mode may omit it, in which case Morpheus creates a bounded synthetic message from the supplied content, reply id, and Discord-CDN attachments. Optional locale and timeZoneId are validated context metadata; command authorization and server time remain authoritative.")] McpCommandInvocation invocation,
        CancellationToken cancellationToken = default) =>
        commandExecution.InvokeAsync(invocation, cancellationToken);

    [McpServerTool(
        Name = "get_activity_overview",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Get aggregate Morpheus activity totals and the last 30 days of activity.")]
    public Task<McpActivityOverview> GetActivityOverviewAsync(
        CancellationToken cancellationToken = default) =>
        service.GetActivityOverviewAsync(cancellationToken);

    [McpServerTool(
        Name = "get_guild_info",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Get aggregate statistics for one Discord guild. Provide an internal guild id or a Discord guild id.")]
    public Task<McpGuildInfo?> GetGuildInfoAsync(
        [Description("Positive internal Morpheus guild id.")] int? guildId = null,
        [Description("Positive Discord guild id, represented as a decimal string.")] string? discordId = null,
        CancellationToken cancellationToken = default)
    {
        ulong? parsedDiscordId = null;
        if (!string.IsNullOrWhiteSpace(discordId))
        {
            if (!ulong.TryParse(discordId, out ulong value) || value == 0)
                throw new ArgumentException("discordId must be a positive decimal Discord id.", nameof(discordId));
            parsedDiscordId = value;
        }

        return service.GetGuildInfoAsync(guildId, parsedDiscordId, cancellationToken);
    }

    [McpServerTool(
        Name = "get_approved_quotes",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Get a page of approved, non-removed quotes. Pending and removed quotes are never returned.")]
    public Task<McpQuotePage> GetApprovedQuotesAsync(
        [Description("Page number, starting at 1.")] int page = 1,
        [Description("Sort order: newest, oldest, or score.")] string sort = "newest",
        [Description("Optional positive internal guild id.")] int? guildId = null,
        CancellationToken cancellationToken = default) =>
        service.GetApprovedQuotesAsync(page, sort, guildId, cancellationToken);

    [McpServerTool(
        Name = "get_approved_quote",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Get one approved, non-removed quote by its internal id.")]
    public Task<McpQuoteDetail?> GetApprovedQuoteAsync(
        [Description("Positive internal quote id.")] int quoteId,
        CancellationToken cancellationToken = default) =>
        service.GetApprovedQuoteAsync(quoteId, cancellationToken);

    [McpServerTool(
        Name = "get_guild_leaderboard",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Get an XP or message leaderboard for one guild and a bounded lookback period.")]
    public Task<IReadOnlyList<McpLeaderboardEntry>> GetGuildLeaderboardAsync(
        [Description("Metric: xp or messages.")] string metric,
        [Description("Positive internal Morpheus guild id.")] int guildId,
        [Description("Lookback period from 1 through 365 days.")] int days = 30,
        [Description("Number of entries from 1 through 50.")] int limit = 10,
        CancellationToken cancellationToken = default) =>
        service.GetLeaderboardAsync(metric, guildId, days, limit, cancellationToken);
}
