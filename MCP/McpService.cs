using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;

namespace Morpheus.MCP;

/// <summary>
/// Read-only, deliberately limited data surface exposed through MCP.
/// </summary>
public sealed class McpService(DB dbContext)
{
    private const int QuotePageSize = 10;

    public async Task<McpGuildInfo?> GetGuildInfoAsync(
        int? guildId,
        ulong? discordId,
        CancellationToken ct = default)
    {
        IQueryable<Guild> query = dbContext.Guilds.AsNoTracking();

        if (guildId is > 0)
            query = query.Where(g => g.Id == guildId.Value);
        else if (discordId is > 0)
            query = query.Where(g => g.DiscordId == discordId.Value);
        else
            throw new ArgumentException("Provide a positive guildId or discordId.");

        Guild? guild = await query.FirstOrDefaultAsync(ct);
        if (guild is null)
            return null;

        var levels = await dbContext.UserLevels
            .AsNoTracking()
            .Where(level => level.GuildId == guild.Id)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Messages = group.Sum(level => (long)level.UserMessageCount),
                Xp = group.Sum(level => (long)level.TotalXp),
                Users = group.Count()
            })
            .FirstOrDefaultAsync(ct);

        int approvedQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote =>
                quote.GuildId == guild.Id && quote.Approved && !quote.Removed,
                ct);

        return new McpGuildInfo(
            guild.Id,
            guild.Name,
            levels?.Users ?? 0,
            levels?.Messages ?? 0,
            levels?.Xp ?? 0,
            approvedQuotes);
    }

    public async Task<McpActivityOverview> GetActivityOverviewAsync(CancellationToken ct = default)
    {
        DateTime last30Days = DateTime.UtcNow.AddDays(-30);

        var levelTotals = await dbContext.UserLevels
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Messages = group.Sum(level => (long)level.UserMessageCount),
                Xp = group.Sum(level => (long)level.TotalXp)
            })
            .FirstOrDefaultAsync(ct);

        IQueryable<UserActivity> recentActivity = dbContext.UserActivity
            .AsNoTracking()
            .Where(activity => activity.InsertDate >= last30Days);

        return new McpActivityOverview(
            levelTotals?.Messages ?? 0,
            levelTotals?.Xp ?? 0,
            await recentActivity.Select(activity => activity.UserId).Distinct().CountAsync(ct),
            await recentActivity.LongCountAsync(ct),
            await recentActivity.SumAsync(activity => (long?)activity.XpGained, ct) ?? 0,
            await dbContext.Guilds.AsNoTracking().CountAsync(ct),
            await dbContext.Users.AsNoTracking().CountAsync(ct));
    }

    public async Task<McpQuotePage> GetApprovedQuotesAsync(
        int page = 1,
        string sort = "newest",
        int? guildId = null,
        CancellationToken ct = default)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");
        if (guildId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(guildId), "Guild id must be greater than zero.");

        IQueryable<Quote> query = dbContext.Quotes
            .AsNoTracking()
            .Where(quote => quote.Approved && !quote.Removed);

        if (guildId.HasValue)
            query = query.Where(quote => quote.GuildId == guildId.Value);

        int total = await query.CountAsync(ct);
        int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)QuotePageSize));
        int effectivePage = Math.Min(page, totalPages);

        query = sort.ToLowerInvariant() switch
        {
            "newest" => query.OrderByDescending(quote => quote.InsertDate).ThenByDescending(quote => quote.Id),
            "oldest" => query.OrderBy(quote => quote.InsertDate).ThenBy(quote => quote.Id),
            "score" => query.OrderByDescending(quote => quote.Scores.Sum(score => (long)score.Score))
                .ThenByDescending(quote => quote.Id),
            _ => throw new ArgumentException("Sort must be newest, oldest, or score.", nameof(sort))
        };

        List<Quote> quotes = await query
            .Skip((effectivePage - 1) * QuotePageSize)
            .Take(QuotePageSize)
            .ToListAsync(ct);

        if (quotes.Count == 0)
            return new McpQuotePage(effectivePage, totalPages, total, []);

        List<int> quoteIds = [.. quotes.Select(quote => quote.Id)];
        Dictionary<int, long> scoreMap = await dbContext.QuoteScores
            .AsNoTracking()
            .Where(score => quoteIds.Contains(score.QuoteId))
            .GroupBy(score => score.QuoteId)
            .Select(group => new { QuoteId = group.Key, Score = group.Sum(score => (long)score.Score) })
            .ToDictionaryAsync(group => group.QuoteId, group => group.Score, ct);

        List<int> userIds = [.. quotes.Select(quote => quote.UserId).Distinct()];
        Dictionary<int, string> userMap = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Username, ct);

        IReadOnlyList<McpQuoteItem> items = [.. quotes.Select(quote => new McpQuoteItem(
            quote.Id,
            quote.GuildId,
            userMap.GetValueOrDefault(quote.UserId, "Unknown"),
            quote.Content ?? string.Empty,
            quote.InsertDate,
            scoreMap.GetValueOrDefault(quote.Id)))];

        return new McpQuotePage(effectivePage, totalPages, total, items);
    }

    public async Task<McpQuoteDetail?> GetApprovedQuoteAsync(
        int quoteId,
        CancellationToken ct = default)
    {
        if (quoteId <= 0)
            throw new ArgumentOutOfRangeException(nameof(quoteId), "Quote id must be greater than zero.");

        Quote? quote = await dbContext.Quotes
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == quoteId && candidate.Approved && !candidate.Removed,
                ct);

        if (quote is null)
            return null;

        long totalScore = await dbContext.QuoteScores
            .AsNoTracking()
            .Where(score => score.QuoteId == quote.Id)
            .SumAsync(score => (long?)score.Score, ct) ?? 0;

        string author = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == quote.UserId)
            .Select(user => user.Username)
            .FirstOrDefaultAsync(ct) ?? "Unknown";

        return new McpQuoteDetail(
            quote.Id,
            quote.GuildId,
            quote.Content ?? string.Empty,
            quote.InsertDate,
            totalScore,
            author);
    }

    public async Task<IReadOnlyList<McpLeaderboardEntry>> GetLeaderboardAsync(
        string metric,
        int guildId,
        int days = 30,
        int limit = 10,
        CancellationToken ct = default)
    {
        if (guildId <= 0)
            throw new ArgumentOutOfRangeException(nameof(guildId), "Guild id must be greater than zero.");
        if (days is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(days), "Days must be between 1 and 365.");
        if (limit is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 50.");

        DateTime since = DateTime.UtcNow.AddDays(-days);
        IQueryable<UserActivity> activityQuery = dbContext.UserActivity
            .AsNoTracking()
            .Where(activity =>
                activity.GuildId == guildId && activity.InsertDate >= since);

        var values = metric.ToLowerInvariant() switch
        {
            "messages" => await activityQuery
                .GroupBy(activity => activity.UserId)
                .Select(group => new { UserId = group.Key, Value = group.LongCount() })
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.UserId)
                .Take(limit)
                .ToListAsync(ct),
            "xp" => await activityQuery
                .GroupBy(activity => activity.UserId)
                .Select(group => new { UserId = group.Key, Value = group.Sum(activity => (long)activity.XpGained) })
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.UserId)
                .Take(limit)
                .ToListAsync(ct),
            _ => throw new ArgumentException("Metric must be xp or messages.", nameof(metric))
        };

        if (values.Count == 0)
            return [];

        List<int> userIds = [.. values.Select(item => item.UserId)];
        Dictionary<int, string> users = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Username, ct);

        Dictionary<int, int?> levels = await dbContext.UserLevels
            .AsNoTracking()
            .Where(level => level.GuildId == guildId && userIds.Contains(level.UserId))
            .ToDictionaryAsync(level => level.UserId, level => (int?)level.Level, ct);

        return [.. values.Select((item, index) => new McpLeaderboardEntry(
            index + 1,
            users.GetValueOrDefault(item.UserId, "Unknown"),
            item.Value,
            levels.GetValueOrDefault(item.UserId)))];
    }
}
