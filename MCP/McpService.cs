using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;

namespace Morpheus.MCP;

/// <summary>
/// Service that provides data for the MCP API endpoints.
/// Wraps database queries to expose bot data to AI agents.
/// </summary>
public sealed class McpService(DB dbContext)
{
    private const string UbiPoolSettingKey = "ubi_pool";
    private const string SlotsVaultSettingKey = "slots_vault";
    private const decimal SlotsVaultDefaultAmount = 10000.00m;

    /// <summary>
    /// Gets stats for a specific user by id or discord id.
    /// </summary>
    public async Task<McpUserStats?> GetUserStatsAsync(int? userId, ulong? discordId, CancellationToken ct = default)
    {
        IQueryable<User> query = dbContext.Users.AsNoTracking();

        if (userId.HasValue)
            query = query.Where(u => u.Id == userId.Value);
        else if (discordId.HasValue)
            query = query.Where(u => u.DiscordId == discordId.Value);
        else
            return null;

        User? user = await query.FirstOrDefaultAsync(ct);
        if (user == null) return null;

        var levels = await dbContext.UserLevels
            .AsNoTracking()
            .Where(ul => ul.UserId == user.Id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Messages = g.Sum(ul => (long)ul.UserMessageCount),
                Xp = g.Sum(ul => (long)ul.TotalXp),
                MaxLevel = g.Max(ul => (int?)ul.Level)
            })
            .FirstOrDefaultAsync(ct);

        int quoteCount = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(q => q.UserId == user.Id && !q.Removed, ct);

        int quoteScore = await dbContext.QuoteScores
            .AsNoTracking()
            .Where(qs => qs.UserId == user.Id)
            .SumAsync(qs => (int?)qs.Score, ct) ?? 0;

        int buttonScore = await dbContext.ButtonGamePresses
            .AsNoTracking()
            .CountAsync(bp => bp.UserId == user.Id, ct);

        DateTime? lastActivity = await dbContext.UserActivity
            .AsNoTracking()
            .Where(ua => ua.UserId == user.Id)
            .Select(ua => (DateTime?)ua.InsertDate)
            .MaxAsync(ct);

        return new McpUserStats(
            user.Id,
            user.DiscordId,
            user.Username,
            user.InsertDate,
            user.Balance,
            (int)(levels?.Messages ?? 0),
            levels?.Xp ?? 0,
            levels?.MaxLevel,
            quoteCount,
            quoteScore,
            buttonScore,
            lastActivity);
    }

    /// <summary>
    /// Gets guild info by id or discord id.
    /// </summary>
    public async Task<McpGuildInfo?> GetGuildInfoAsync(int? guildId, ulong? discordId, CancellationToken ct = default)
    {
        IQueryable<Guild> query = dbContext.Guilds.AsNoTracking();

        if (guildId.HasValue)
            query = query.Where(g => g.Id == guildId.Value);
        else if (discordId.HasValue)
            query = query.Where(g => g.DiscordId == discordId.Value);
        else
            return null;

        Guild? guild = await query.FirstOrDefaultAsync(ct);
        if (guild == null) return null;

        var levels = await dbContext.UserLevels
            .AsNoTracking()
            .Where(ul => ul.GuildId == guild.Id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Messages = g.Sum(ul => (long)ul.UserMessageCount),
                Xp = g.Sum(ul => (long)ul.TotalXp),
                Users = g.Count()
            })
            .FirstOrDefaultAsync(ct);

        int approvedQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(q => q.GuildId == guild.Id && q.Approved && !q.Removed, ct);

        return new McpGuildInfo(
            guild.Id,
            guild.DiscordId,
            guild.Name,
            guild.InsertDate,
            guild.Prefix,
            levels?.Users ?? 0,
            levels?.Messages ?? 0,
            levels?.Xp ?? 0,
            approvedQuotes,
            guild.UseGlobalQuotes,
            guild.WelcomeMessages,
            guild.UseActivityRoles);
    }

    /// <summary>
    /// Gets economy summary: total balances, pool, vault, stocks.
    /// </summary>
    public async Task<McpEconomySummary> GetEconomySummaryAsync(CancellationToken ct = default)
    {
        int totalUsers = await dbContext.Users.AsNoTracking().CountAsync(ct);
        decimal totalBalance = (await dbContext.Users
            .AsNoTracking()
            .Select(u => u.Balance)
            .ToListAsync(ct))
            .Sum();

        decimal averageBalance = totalUsers > 0 ? totalBalance / totalUsers : 0m;

        decimal ubiPool = await GetBotSettingDecimalAsync(UbiPoolSettingKey, 0m, ct);
        decimal slotsVault = await GetBotSettingDecimalAsync(SlotsVaultSettingKey, SlotsVaultDefaultAmount, ct);

        int totalStocks = await dbContext.Stocks.AsNoTracking().CountAsync(ct);
        decimal totalPortfolio = (await dbContext.StockHoldings
            .AsNoTracking()
            .Include(sh => sh.Stock)
            .Select(sh => sh.Shares * sh.Stock!.Price)
            .ToListAsync(ct))
            .Sum();

        return new McpEconomySummary(
            totalUsers,
            totalBalance,
            Math.Round(averageBalance, 2),
            ubiPool,
            slotsVault,
            totalStocks,
            Math.Round(totalPortfolio, 2));
    }

    /// <summary>
    /// Gets a global activity overview.
    /// </summary>
    public async Task<McpActivityOverview> GetActivityOverviewAsync(CancellationToken ct = default)
    {
        DateTime last30Days = DateTime.UtcNow.AddDays(-30);

        var levelTotals = await dbContext.UserLevels
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Messages = g.Sum(ul => (long)ul.UserMessageCount),
                Xp = g.Sum(ul => (long)ul.TotalXp)
            })
            .FirstOrDefaultAsync(ct);

        long totalMessages = levelTotals?.Messages ?? 0L;
        long totalXp = levelTotals?.Xp ?? 0L;

        IQueryable<UserActivity> recentActivity = dbContext.UserActivity
            .AsNoTracking()
            .Where(ua => ua.InsertDate >= last30Days);

        int activeUsers = await recentActivity
            .Select(ua => ua.UserId)
            .Distinct()
            .CountAsync(ct);

        long messagesLast30Days = await recentActivity.LongCountAsync(ct);
        long xpLast30Days = await recentActivity
            .SumAsync(ua => (long?)ua.XpGained, ct) ?? 0L;

        int totalServers = await dbContext.Guilds.AsNoTracking().CountAsync(ct);
        int totalUsers = await dbContext.Users.AsNoTracking().CountAsync(ct);

        DateTime? lastActivity = await dbContext.UserActivity
            .AsNoTracking()
            .Select(ua => (DateTime?)ua.InsertDate)
            .MaxAsync(ct);

        return new McpActivityOverview(
            totalMessages,
            totalXp,
            activeUsers,
            messagesLast30Days,
            xpLast30Days,
            totalServers,
            totalUsers,
            lastActivity);
    }

    /// <summary>
    /// Gets a list of all servers/guilds.
    /// </summary>
    public async Task<IReadOnlyList<McpServerItem>> GetGuildsAsync(CancellationToken ct = default)
    {
        var guilds = await dbContext.Guilds
            .AsNoTracking()
            .OrderByDescending(g => g.InsertDate)
            .ToListAsync(ct);

        var result = new List<McpServerItem>(guilds.Count);
        foreach (Guild guild in guilds)
        {
            var levels = await dbContext.UserLevels
                .AsNoTracking()
                .Where(ul => ul.GuildId == guild.Id)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Messages = g.Sum(ul => (long)ul.UserMessageCount),
                    Xp = g.Sum(ul => (long)ul.TotalXp),
                    Users = g.Count()
                })
                .FirstOrDefaultAsync(ct);

            int approvedQuotes = await dbContext.Quotes
                .AsNoTracking()
                .CountAsync(q => q.GuildId == guild.Id && q.Approved && !q.Removed, ct);

            result.Add(new McpServerItem(
                guild.Id,
                guild.DiscordId,
                guild.Name,
                guild.InsertDate,
                levels?.Users ?? 0,
                levels?.Messages ?? 0,
                levels?.Xp ?? 0,
                approvedQuotes));
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Gets a paginated list of users.
    /// </summary>
    public async Task<IReadOnlyList<McpUserItem>> GetUsersAsync(int page = 1, int limit = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (limit < 1) limit = 20;
        if (limit > 100) limit = 100;

        var users = await dbContext.Users
            .AsNoTracking()
            .OrderByDescending(u => u.InsertDate)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(ct);

        var result = new List<McpUserItem>(users.Count);
        foreach (User user in users)
        {
            var levels = await dbContext.UserLevels
                .AsNoTracking()
                .Where(ul => ul.UserId == user.Id)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Messages = g.Sum(ul => (long)ul.UserMessageCount),
                    Xp = g.Sum(ul => (long)ul.TotalXp),
                    MaxLevel = g.Max(ul => (int?)ul.Level)
                })
                .FirstOrDefaultAsync(ct);

            result.Add(new McpUserItem(
                user.Id,
                user.DiscordId,
                user.Username,
                user.InsertDate,
                user.Balance,
                levels?.Messages ?? 0,
                levels?.Xp ?? 0,
                levels?.MaxLevel));
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Gets a page of quotes.
    /// </summary>
    public async Task<McpQuotePage> GetQuotesAsync(
        int page = 1,
        string sort = "newest",
        bool approvedOnly = true,
        int? guildId = null,
        CancellationToken ct = default)
    {
        IQueryable<Quote> query = dbContext.Quotes.AsNoTracking().Where(q => !q.Removed);

        if (guildId.HasValue)
            query = query.Where(q => q.GuildId == guildId.Value);

        if (approvedOnly)
            query = query.Where(q => q.Approved);

        int total = await query.CountAsync(ct);
        int totalPages = (int)Math.Ceiling(total / (double)10);
        if (totalPages == 0) totalPages = 1;

        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        query = sort.ToLowerInvariant() switch
        {
            "oldest" => query.OrderBy(q => q.InsertDate),
            "score" => query.OrderByDescending(q => q.Scores.Sum(s => (int)s.Score)),
            _ => query.OrderByDescending(q => q.InsertDate),
        };

        List<Quote> quotes = await query
            .Skip((page - 1) * 10)
            .Take(10)
            .ToListAsync(ct);

        if (quotes.Count == 0)
            return new McpQuotePage(page, totalPages, total, []);

        List<int> quoteIds = [.. quotes.Select(q => q.Id)];
        Dictionary<int, int> scoreMap = await dbContext.QuoteScores
            .AsNoTracking()
            .Where(qs => quoteIds.Contains(qs.QuoteId))
            .GroupBy(qs => qs.QuoteId)
            .Select(g => new { QuoteId = g.Key, Score = g.Sum(qs => qs.Score) })
            .ToDictionaryAsync(g => g.QuoteId, g => g.Score, ct);

        List<int> userIds = [.. quotes.Select(q => q.UserId).Distinct()];
        Dictionary<int, string> userMap = await dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username, ct);

        var items = quotes.Select(q => new McpQuoteItem(
            q.Id,
            q.GuildId,
            q.UserId,
            userMap.GetValueOrDefault(q.UserId, "Unknown"),
            q.Content ?? string.Empty,
            q.InsertDate,
            q.Approved,
            q.Removed,
            scoreMap.GetValueOrDefault(q.Id)
        )).ToList();

        return new McpQuotePage(page, totalPages, total, items.AsReadOnly());
    }

    /// <summary>
    /// Gets details for a single quote.
    /// </summary>
    public async Task<McpQuoteDetail?> GetQuoteByIdAsync(int quoteId, CancellationToken ct = default)
    {
        Quote? quote = await dbContext.Quotes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quoteId && !q.Removed, ct);

        if (quote == null) return null;

        int totalScore = await dbContext.QuoteScores
            .AsNoTracking()
            .Where(qs => qs.QuoteId == quote.Id)
            .SumAsync(qs => (int?)qs.Score, ct) ?? 0;

        string author = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == quote.UserId)
            .Select(u => u.Username)
            .FirstOrDefaultAsync(ct) ?? "Unknown";

        return new McpQuoteDetail(
            quote.Id,
            quote.GuildId,
            quote.Content ?? string.Empty,
            quote.InsertDate,
            quote.Approved,
            quote.Removed,
            totalScore,
            author);
    }

    /// <summary>
    /// Gets recent moderation/relevant log entries.
    /// </summary>
    public async Task<IReadOnlyList<McpModerationEntry>> GetRecentLogsAsync(
        int limit = 20,
        string? severity = null,
        CancellationToken ct = default)
    {
        IQueryable<Log> query = dbContext.Logs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(severity))
        {
            if (Enum.TryParse<Discord.LogSeverity>(severity, true, out var parsedSeverity))
                query = query.Where(l => l.Severity == (int)parsedSeverity);
        }

        if (limit < 1) limit = 20;
        if (limit > 100) limit = 100;

        var logs = await query
            .OrderByDescending(l => l.InsertDate)
            .Take(limit)
            .ToListAsync(ct);

        return logs.Select(l => new McpModerationEntry(
            l.Id,
            ((Discord.LogSeverity)l.Severity).ToString(),
            l.Message,
            l.InsertDate
        )).ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets stock market summary with top gainers and losers.
    /// </summary>
    public async Task<McpStockSummary> GetStockSummaryAsync(int moverLimit = 5, CancellationToken ct = default)
    {
        int totalStocks = await dbContext.Stocks.AsNoTracking().CountAsync(ct);

        var stocks = await dbContext.Stocks
            .AsNoTracking()
            .Where(s => s.Price > 0)
            .ToListAsync(ct);

        var gainers = stocks
            .Where(s => s.DailyChangePercent > 0)
            .OrderByDescending(s => s.DailyChangePercent)
            .Take(moverLimit)
            .Select(s => new McpStockItem(
                s.Id,
                s.EntityType.ToString(),
                s.EntityId,
                ResolveStockName(s),
                Math.Round(s.Price, 2),
                Math.Round(s.DailyChangePercent, 2)))
            .ToList().AsReadOnly();

        var losers = stocks
            .Where(s => s.DailyChangePercent < 0)
            .OrderBy(s => s.DailyChangePercent)
            .Take(moverLimit)
            .Select(s => new McpStockItem(
                s.Id,
                s.EntityType.ToString(),
                s.EntityId,
                ResolveStockName(s),
                Math.Round(s.Price, 2),
                Math.Round(s.DailyChangePercent, 2)))
            .ToList().AsReadOnly();

        return new McpStockSummary(totalStocks, gainers, losers);
    }

    /// <summary>
    /// Gets activity leaderboard data.
    /// </summary>
    public async Task<IReadOnlyList<McpLeaderboardEntry>> GetLeaderboardAsync(
        string metric = "xp",
        int? guildId = null,
        int days = 30,
        int limit = 10,
        CancellationToken ct = default)
    {
        if (limit < 1) limit = 10;
        if (limit > 50) limit = 50;

        DateTime since = DateTime.UtcNow.AddDays(-days);

        IQueryable<UserActivity> activityQuery = dbContext.UserActivity
            .AsNoTracking()
            .Where(ua => ua.InsertDate >= since);

        if (guildId.HasValue)
            activityQuery = activityQuery.Where(ua => ua.GuildId == guildId.Value);

        var raw = metric.ToLowerInvariant() switch
        {
            "messages" => await activityQuery
                .GroupBy(ua => ua.UserId)
                .Select(g => new { UserId = g.Key, Value = g.LongCount() })
                .OrderByDescending(x => x.Value)
                .Take(limit)
                .ToListAsync(ct),
            _ => await activityQuery
                .GroupBy(ua => ua.UserId)
                .Select(g => new { UserId = g.Key, Value = g.Sum(ua => (long)ua.XpGained) })
                .OrderByDescending(x => x.Value)
                .Take(limit)
                .ToListAsync(ct)
        };

        if (raw.Count == 0)
            return [];

        List<int> userIds = [.. raw.Select(x => x.UserId)];
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var levels = await dbContext.UserLevels
            .AsNoTracking()
            .Where(ul => userIds.Contains(ul.UserId))
            .GroupBy(ul => ul.UserId)
            .Select(g => new { UserId = g.Key, MaxLevel = g.Max(ul => (int?)ul.Level) })
            .ToDictionaryAsync(g => g.UserId, g => g.MaxLevel, ct);

        return raw.Select((x, i) => new McpLeaderboardEntry(
            i + 1,
            x.UserId,
            users.GetValueOrDefault(x.UserId)?.DiscordId ?? 0,
            users.GetValueOrDefault(x.UserId)?.Username ?? "Unknown",
            x.Value,
            levels.GetValueOrDefault(x.UserId)
        )).ToList().AsReadOnly();
    }

    private async Task<decimal> GetBotSettingDecimalAsync(string key, decimal defaultValue, CancellationToken ct)
    {
        BotSetting? setting = await dbContext.BotSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
            return defaultValue;

        return decimal.TryParse(setting.Value, out decimal val) ? val : defaultValue;
    }

    private static string ResolveStockName(Stock stock)
    {
        // For stocks tied to entities, try to provide a meaningful name.
        // If the entity isn't loaded, fall back to the stock id.
        return $"Stock #{stock.Id} ({stock.EntityType})";
    }
}