using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Morpheus.Utilities;

namespace Morpheus.Dashboard;

public sealed class DashboardStatsService(DB dbContext, DashboardApiOptions options)
{
    private const int DefaultLeaderboardLimit = 10;
    private const int MaxLeaderboardLimit = 50;
    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";

    public async Task<DashboardOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;
        DateTime last24Hours = now.AddDays(-1);
        DateTime last30Days = now.AddDays(-30);

        int guildCount = await dbContext.Guilds.AsNoTracking().CountAsync(cancellationToken);
        int userCount = await dbContext.Users.AsNoTracking().CountAsync(cancellationToken);
        int stockCount = await dbContext.Stocks.AsNoTracking().CountAsync(cancellationToken);

        long totalMessages = await dbContext.UserLevels
            .AsNoTracking()
            .SumAsync(levels => (long?)levels.UserMessageCount, cancellationToken) ?? 0L;
        long totalXp = await dbContext.UserLevels
            .AsNoTracking()
            .SumAsync(levels => (long?)levels.TotalXp, cancellationToken) ?? 0L;

        IQueryable<UserActivity> recentActivity = dbContext.UserActivity
            .AsNoTracking()
            .Where(activity => activity.InsertDate >= last30Days);

        int activeUsersLast30Days = await recentActivity
            .Select(activity => activity.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
        long messagesLast30Days = await recentActivity.LongCountAsync(cancellationToken);
        long xpLast30Days = await recentActivity
            .SumAsync(activity => (long?)activity.XpGained, cancellationToken) ?? 0L;
        DateTime? lastActivityAtUtc = await dbContext.UserActivity
            .AsNoTracking()
            .Select(activity => (DateTime?)activity.InsertDate)
            .MaxAsync(cancellationToken);

        int approvedQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote => quote.Approved && !quote.Removed, cancellationToken);
        int pendingQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote => !quote.Approved && !quote.Removed, cancellationToken);
        int removedQuotes = await dbContext.Quotes
            .AsNoTracking()
            .CountAsync(quote => quote.Removed, cancellationToken);
        int totalQuoteScore = await dbContext.QuoteScores
            .AsNoTracking()
            .SumAsync(score => (int?)score.Score, cancellationToken) ?? 0;

        decimal totalBalance = await GetTotalBalanceAsync(cancellationToken);
        decimal stockPortfolioValue = await GetStockPortfolioValueAsync(cancellationToken);

        long totalLogs = await dbContext.Logs.AsNoTracking().LongCountAsync(cancellationToken);
        int logsLast24Hours = await dbContext.Logs
            .AsNoTracking()
            .CountAsync(log => log.InsertDate >= last24Hours, cancellationToken);
        DateTime? lastLogAtUtc = await dbContext.Logs
            .AsNoTracking()
            .Select(log => (DateTime?)log.InsertDate)
            .MaxAsync(cancellationToken);

        return new DashboardOverviewResponse(
            now,
            Env.StartTime,
            (long)(now - Env.StartTime).TotalSeconds,
            new DashboardSystemStats(guildCount, userCount, stockCount),
            new DashboardActivityStats(
                totalMessages,
                totalXp,
                activeUsersLast30Days,
                messagesLast30Days,
                xpLast30Days,
                lastActivityAtUtc),
            new DashboardQuoteStats(approvedQuotes, pendingQuotes, removedQuotes, totalQuoteScore),
            new DashboardEconomyStats(totalBalance, stockPortfolioValue),
            new DashboardLogStats(totalLogs, logsLast24Hours, lastLogAtUtc));
    }

    public async Task<IReadOnlyList<DashboardGuildSummary>> GetGuildsAsync(CancellationToken cancellationToken = default)
    {
        var guilds = await dbContext.Guilds
            .AsNoTracking()
            .OrderBy(guild => guild.Name)
            .Select(guild => new
            {
                guild.Id,
                guild.DiscordId,
                guild.Name,
                guild.InsertDate,
                TrackedUsers = dbContext.UserLevels.Count(levels => levels.GuildId == guild.Id),
                Messages = dbContext.UserActivity.LongCount(activity => activity.GuildId == guild.Id),
                Xp = dbContext.UserActivity
                    .Where(activity => activity.GuildId == guild.Id)
                    .Sum(activity => (long?)activity.XpGained) ?? 0L,
                ApprovedQuotes = dbContext.Quotes.Count(quote =>
                    quote.GuildId == guild.Id &&
                    quote.Approved &&
                    !quote.Removed)
            })
            .ToListAsync(cancellationToken);

        return [.. guilds.Select(guild => new DashboardGuildSummary(
            guild.Id,
            guild.DiscordId.ToString(),
            guild.Name,
            guild.InsertDate,
            guild.TrackedUsers,
            guild.Messages,
            guild.Xp,
            guild.ApprovedQuotes))];
    }

    public async Task<DashboardActivitySeriesResponse> GetActivitySeriesAsync(
        int? guildId,
        int days,
        CancellationToken cancellationToken = default)
    {
        int safeDays = ClampDays(days);
        DateTime startDate = DateTime.UtcNow.Date.AddDays(-(safeDays - 1));

        IQueryable<UserActivity> query = dbContext.UserActivity
            .AsNoTracking()
            .Where(activity => activity.InsertDate >= startDate);

        if (guildId.HasValue)
            query = query.Where(activity => activity.GuildId == guildId.Value);

        var rows = await query
            .GroupBy(activity => activity.InsertDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                Messages = group.Count(),
                Xp = group.Sum(activity => (long)activity.XpGained),
                ActiveUsers = group.Select(activity => activity.UserId).Distinct().Count(),
                AverageMessageLength = group.Average(activity => (double?)activity.MessageLength) ?? 0.0
            })
            .OrderBy(row => row.Date)
            .ToListAsync(cancellationToken);

        Dictionary<DateTime, DashboardActivityPoint> pointsByDate = rows.ToDictionary(
            row => row.Date.Date,
            row => new DashboardActivityPoint(
                DateTime.SpecifyKind(row.Date.Date, DateTimeKind.Utc),
                row.Messages,
                row.Xp,
                row.ActiveUsers,
                Math.Round(row.AverageMessageLength, 1)));

        List<DashboardActivityPoint> points = [];
        for (int offset = 0; offset < safeDays; offset++)
        {
            DateTime date = startDate.AddDays(offset);
            points.Add(pointsByDate.TryGetValue(date, out DashboardActivityPoint? point)
                ? point
                : new DashboardActivityPoint(DateTime.SpecifyKind(date, DateTimeKind.Utc), 0, 0, 0, 0.0));
        }

        return new DashboardActivitySeriesResponse(guildId, safeDays, points);
    }

    public async Task<DashboardLeaderboardResponse> GetActivityLeaderboardAsync(
        int? guildId,
        string metric,
        int? days,
        int limit,
        CancellationToken cancellationToken = default)
    {
        string normalizedMetric = NormalizeMetric(metric);
        int safeLimit = ClampLimit(limit);
        int? safeDays = days is > 0 ? ClampDays(days.Value) : null;

        List<DashboardLeaderboardRow> rows = safeDays.HasValue
            ? await GetRecentLeaderboardRowsAsync(guildId, normalizedMetric, safeDays.Value, safeLimit, cancellationToken)
            : await GetAllTimeLeaderboardRowsAsync(guildId, normalizedMetric, safeLimit, cancellationToken);

        Dictionary<int, (string DiscordId, string Username)> users = await GetUserLabelsAsync(
            rows.Select(row => row.UserId),
            cancellationToken);

        List<DashboardLeaderboardItem> items =
        [
            .. rows.Select((row, index) =>
            {
                (string discordId, string username) = users.GetValueOrDefault(row.UserId, (string.Empty, "Unknown"));
                int? level = normalizedMetric == "xp"
                    ? ActivityLevelService.CalculateLevel(row.Value)
                    : null;

                return new DashboardLeaderboardItem(
                    index + 1,
                    row.UserId,
                    discordId,
                    username,
                    row.Value,
                    level,
                    row.LastActivityAtUtc);
            })
        ];

        return new DashboardLeaderboardResponse(guildId, normalizedMetric, safeDays, safeLimit, items);
    }

    private async Task<List<DashboardLeaderboardRow>> GetRecentLeaderboardRowsAsync(
        int? guildId,
        string metric,
        int days,
        int limit,
        CancellationToken cancellationToken)
    {
        DateTime since = DateTime.UtcNow.AddDays(-days);
        IQueryable<UserActivity> query = dbContext.UserActivity
            .AsNoTracking()
            .Where(activity => activity.InsertDate >= since);

        if (guildId.HasValue)
            query = query.Where(activity => activity.GuildId == guildId.Value);

        if (metric == "messages")
        {
            var rows = await query
                .GroupBy(activity => activity.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Value = group.LongCount(),
                    LastActivityAtUtc = group.Max(activity => activity.InsertDate)
                })
                .OrderByDescending(row => row.Value)
                .ThenByDescending(row => row.LastActivityAtUtc)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return [.. rows.Select(row => new DashboardLeaderboardRow(row.UserId, row.Value, row.LastActivityAtUtc))];
        }

        var xpRows = await query
            .GroupBy(activity => activity.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Value = group.Sum(activity => (long)activity.XpGained),
                LastActivityAtUtc = group.Max(activity => activity.InsertDate)
            })
            .OrderByDescending(row => row.Value)
            .ThenByDescending(row => row.LastActivityAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return [.. xpRows.Select(row => new DashboardLeaderboardRow(row.UserId, row.Value, row.LastActivityAtUtc))];
    }

    private async Task<List<DashboardLeaderboardRow>> GetAllTimeLeaderboardRowsAsync(
        int? guildId,
        string metric,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<UserLevels> query = dbContext.UserLevels.AsNoTracking();

        if (guildId.HasValue)
            query = query.Where(levels => levels.GuildId == guildId.Value);

        if (metric == "messages")
        {
            var rows = await query
                .GroupBy(levels => levels.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    Value = group.Sum(levels => (long)levels.UserMessageCount)
                })
                .Where(row => row.Value > 0)
                .OrderByDescending(row => row.Value)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return [.. rows.Select(row => new DashboardLeaderboardRow(row.UserId, row.Value, null))];
        }

        var xpRows = await query
            .GroupBy(levels => levels.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Value = group.Sum(levels => (long)levels.TotalXp)
            })
            .Where(row => row.Value > 0)
            .OrderByDescending(row => row.Value)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return [.. xpRows.Select(row => new DashboardLeaderboardRow(row.UserId, row.Value, null))];
    }

    private async Task<Dictionary<int, (string DiscordId, string Username)>> GetUserLabelsAsync(
        IEnumerable<int> userIds,
        CancellationToken cancellationToken)
    {
        List<int> ids = [.. userIds.Distinct()];

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.DiscordId, user.Username })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            user => user.Id,
            user => (user.DiscordId.ToString(), user.Username));
    }

    private async Task<decimal> GetTotalBalanceAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.ProviderName == SqliteProviderName)
        {
            List<decimal> balances = await dbContext.Users
                .AsNoTracking()
                .Select(user => user.Balance)
                .ToListAsync(cancellationToken);

            return balances.Sum();
        }

        return await dbContext.Users
            .AsNoTracking()
            .SumAsync(user => (decimal?)user.Balance, cancellationToken) ?? 0m;
    }

    private async Task<decimal> GetStockPortfolioValueAsync(CancellationToken cancellationToken)
    {
        var holdingValues = dbContext.StockHoldings
            .AsNoTracking()
            .Join(
                dbContext.Stocks.AsNoTracking(),
                holding => holding.StockId,
                stock => stock.Id,
                (holding, stock) => new { holding.Shares, stock.Price });

        if (dbContext.Database.ProviderName == SqliteProviderName)
        {
            var sqliteValues = await holdingValues.ToListAsync(cancellationToken);
            return sqliteValues.Sum(holding => holding.Shares * holding.Price);
        }

        return await holdingValues
            .SumAsync(holding => (decimal?)(holding.Shares * holding.Price), cancellationToken) ?? 0m;
    }

    private int ClampDays(int days)
    {
        int maxDays = Math.Max(1, options.MaxActivityDays);
        return Math.Clamp(days, 1, maxDays);
    }

    private static int ClampLimit(int limit)
    {
        if (limit <= 0)
            return DefaultLeaderboardLimit;

        return Math.Clamp(limit, 1, MaxLeaderboardLimit);
    }

    private static string NormalizeMetric(string metric) =>
        metric.Trim().ToLowerInvariant() switch
        {
            "message" or "messages" => "messages",
            _ => "xp"
        };
}
