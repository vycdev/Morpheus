using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using System.Globalization;

namespace Morpheus.Services;

public class ActivityGraphService(DB dbContext)
{
    public static ActivityGraphParseResult ParseDaysString(string? input, bool isOwner, int maxDays)
    {
        if (string.IsNullOrWhiteSpace(input))
            input = "past7days";

        input = input.Trim().ToLowerInvariant();

        if (input.StartsWith("past") && input.EndsWith("days"))
        {
            string num = input[4..^4];
            if (int.TryParse(num, out int parsed))
                return ActivityGraphParseResult.Valid(NormalizeDayCount(parsed, isOwner, maxDays), null);

            return ActivityGraphParseResult.Error(
                $"Please provide a number of days between 7 and {maxDays} or a valid preset (past7days, past30days, past60days, past{maxDays}days).\nOr provide a date range like 2025-01-01..2025-01-31.");
        }

        if (int.TryParse(input, out int asInt))
            return ActivityGraphParseResult.Valid(NormalizeDayCount(asInt, isOwner, maxDays), null);

        if (input.Contains(".."))
            return ParseDateRange(input, isOwner, maxDays);

        return ActivityGraphParseResult.Error(
            "Unrecognized days parameter. Use presets (past7days) or integer days or a date range (YYYY-MM-DD..YYYY-MM-DD). Default is past7days.");
    }

    public static ActivityGraphRange ResolveRange(ActivityGraphParseResult parse, DateTime? utcNow = null)
    {
        DateTime start = parse.ExplicitStart ?? GetStartDate(parse.Days, utcNow);
        return new ActivityGraphRange(parse.Days, NormalizeToUtc(start), parse.ExplicitStart);
    }

    public async Task<ActivityGraphBuildResult> BuildUserActivityGraphAsync(
        ActivityGraphRange range,
        int? guildId,
        bool global,
        IEnumerable<ulong> mentionedDiscordIds,
        bool cumulative,
        int? rollingWindowDays)
    {
        List<UserActivityAggregate> perUser = mentionedDiscordIds.Any()
            ? await GetTopUsersByWindowForMentionsAsync(range.Start, range.Days, guildId, global, mentionedDiscordIds)
            : await GetTopUsersByWindowAsync(range.Start, range.Days, guildId, global);

        Dictionary<string, List<int>> series = await BuildUserSeriesAsync(perUser, range.Start, range.Days, cumulative, global, guildId);
        if (rollingWindowDays.HasValue)
            series = RollingAverage(series, rollingWindowDays.Value);

        string message = BuildUserGraphMessage(series.Count, range, global, cumulative, rollingWindowDays);
        return new ActivityGraphBuildResult(series, range.Days, range.Start, message);
    }

    public async Task<ActivityGraphBuildResult> BuildGuildActivityGraphAsync(
        ActivityGraphRange range,
        int guildId,
        bool cumulative,
        int? rollingWindowDays)
    {
        Dictionary<DateTime, int> dailyAggregate = await GetGuildAggregateByDayAsync(range.Start, range.Days, guildId);
        Dictionary<string, List<int>> series = await BuildGuildSeriesAsync(dailyAggregate, range.Start, range.Days, cumulative, guildId);
        if (rollingWindowDays.HasValue)
            series = RollingAverage(series, rollingWindowDays.Value);

        string message = BuildGuildGraphMessage(range, cumulative, rollingWindowDays);
        return new ActivityGraphBuildResult(series, range.Days, range.Start, message);
    }

    internal static DateTime GetStartDate(int days, DateTime? utcNow = null) =>
        (utcNow ?? DateTime.UtcNow).Date.AddDays(-(days - 1));

    internal static DateTime NormalizeToUtc(DateTime dt) => DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    internal static Dictionary<string, List<int>> RollingAverage(Dictionary<string, List<int>> series, int windowDays)
    {
        Dictionary<string, List<int>> outSeries = [];

        foreach ((string label, List<int> values) in series)
        {
            List<int> average = new(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                int start = Math.Max(0, i - (windowDays - 1));
                int length = i - start + 1;
                double sum = 0;

                for (int j = start; j <= i; j++)
                    sum += values[j];

                average.Add((int)Math.Round(sum / length));
            }

            outSeries[label] = average;
        }

        return outSeries;
    }

    internal static string BuildUserGraphMessage(
        int seriesCount,
        ActivityGraphRange range,
        bool global,
        bool cumulative,
        int? rollingWindowDays)
    {
        string descriptor = global ? "global " : string.Empty;
        descriptor += rollingWindowDays.HasValue
            ? $"{rollingWindowDays.Value}-day rolling average "
            : cumulative ? "cumulative " : string.Empty;
        descriptor += "activity";

        return BuildGraphMessage($"Top {seriesCount} users {descriptor}", range);
    }

    internal static string BuildGuildGraphMessage(ActivityGraphRange range, bool cumulative, int? rollingWindowDays)
    {
        string descriptor = "Guild ";
        descriptor += rollingWindowDays.HasValue
            ? $"{rollingWindowDays.Value}-day rolling average "
            : cumulative ? "cumulative " : string.Empty;
        descriptor += "activity";

        return BuildGraphMessage(descriptor, range);
    }

    private static ActivityGraphParseResult ParseDateRange(string input, bool isOwner, int maxDays)
    {
        string[] parts = input.Split([".."], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !DateTime.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime start) ||
            !DateTime.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime end))
        {
            return ActivityGraphParseResult.Error(
                $"Invalid date range format. Use YYYY-MM-DD..YYYY-MM-DD and ensure the range is at most {maxDays} days and start <= end.");
        }

        start = start.Date;
        end = end.Date;
        if (end < start)
            (start, end) = (end, start);

        double span = (end - start).TotalDays + 1;
        if (span < 7)
        {
            end = start.AddDays(6);
            span = 7;
        }

        if (!isOwner && span > maxDays)
            return ActivityGraphParseResult.Error($"Date range exceeds maximum of {maxDays} days.");

        return ActivityGraphParseResult.Valid((int)span, NormalizeToUtc(start));
    }

    private static int NormalizeDayCount(int days, bool isOwner, int maxDays)
    {
        if (days < 7)
            days = 7;

        if (!isOwner && days > maxDays)
            days = maxDays;

        return days;
    }

    private static string BuildGraphMessage(string subject, ActivityGraphRange range)
    {
        if (range.ExplicitStart.HasValue)
            return $"{subject} from {range.ExplicitStart.Value:yyyy-MM-dd} to {range.End:yyyy-MM-dd} ({range.Days} days)";

        return $"{subject} for the last {range.Days} days";
    }

    private async Task<List<UserActivityAggregate>> GetTopUsersByWindowAsync(DateTime start, int days, int? guildId, bool global)
    {
        IQueryable<UserActivity> query = GetActivityQuery(start, days, guildId, global);

        var top = await query
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(x => x.XpGained) })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToListAsync();

        List<int> userIds = [.. top.Select(t => t.UserId)];
        if (userIds.Count == 0)
            return [];

        Dictionary<int, Dictionary<DateTime, int>> byDay = await GetUserActivityByDayAsync(query.Where(ua => userIds.Contains(ua.UserId)));

        return [.. top.Select(t => new UserActivityAggregate(
            t.UserId,
            (int)t.Total,
            byDay.TryGetValue(t.UserId, out Dictionary<DateTime, int>? days)
                ? days
                : new Dictionary<DateTime, int>()))];
    }

    private async Task<List<UserActivityAggregate>> GetTopUsersByWindowForMentionsAsync(
        DateTime start,
        int days,
        int? guildId,
        bool global,
        IEnumerable<ulong> mentionedDiscordIds)
    {
        List<long> discordIds = [.. mentionedDiscordIds.Select(id => (long)id).Distinct()];
        if (discordIds.Count == 0)
            return [];

        List<int> userIds = await dbContext.Users
            .AsNoTracking()
            .Where(u => discordIds.Contains((long)u.DiscordId))
            .Select(u => u.Id)
            .ToListAsync();

        if (userIds.Count == 0)
            return [];

        IQueryable<UserActivity> query = GetActivityQuery(start, days, guildId, global)
            .Where(ua => userIds.Contains(ua.UserId));

        var totals = await query
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(x => x.XpGained) })
            .OrderByDescending(x => x.Total)
            .ToListAsync();

        Dictionary<int, Dictionary<DateTime, int>> byDay = await GetUserActivityByDayAsync(query);

        return [.. totals.Select(t => new UserActivityAggregate(
            t.UserId,
            (int)t.Total,
            byDay.TryGetValue(t.UserId, out Dictionary<DateTime, int>? days)
                ? days
                : new Dictionary<DateTime, int>()))];
    }

    private IQueryable<UserActivity> GetActivityQuery(DateTime start, int days, int? guildId, bool global)
    {
        DateTime endExclusive = start.AddDays(days);
        IQueryable<UserActivity> query = dbContext.UserActivity
            .AsNoTracking()
            .Where(ua => ua.InsertDate >= start && ua.InsertDate < endExclusive);

        if (!global && guildId.HasValue)
            query = query.Where(ua => ua.GuildId == guildId.Value);

        return query;
    }

    private static async Task<Dictionary<int, Dictionary<DateTime, int>>> GetUserActivityByDayAsync(IQueryable<UserActivity> query)
    {
        var byDay = await query
            .GroupBy(ua => new { ua.UserId, Day = ua.InsertDate.Date })
            .Select(g => new { g.Key.UserId, g.Key.Day, Xp = g.Sum(x => x.XpGained) })
            .ToListAsync();

        return byDay
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Day, x => (int)x.Xp));
    }

    private async Task<Dictionary<string, List<int>>> BuildUserSeriesAsync(
        List<UserActivityAggregate> perUser,
        DateTime start,
        int days,
        bool cumulative,
        bool global,
        int? guildId)
    {
        if (perUser.Count == 0)
            return [];

        Dictionary<int, long> baselineMap = [];
        List<int> userIds = [.. perUser.Select(user => user.UserId)];

        if (cumulative)
        {
            IQueryable<UserActivity> baseQuery = dbContext.UserActivity
                .AsNoTracking()
                .Where(ua => ua.InsertDate < start && userIds.Contains(ua.UserId));

            if (!global && guildId.HasValue)
                baseQuery = baseQuery.Where(ua => ua.GuildId == guildId.Value);

            var baseList = await baseQuery
                .GroupBy(ua => ua.UserId)
                .Select(g => new { UserId = g.Key, Baseline = (long)g.Sum(x => x.XpGained) })
                .ToListAsync();

            baselineMap = baseList.ToDictionary(x => x.UserId, x => x.Baseline);
        }

        Dictionary<int, string> names = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Username })
            .ToDictionaryAsync(user => user.Id, user => user.Username);

        Dictionary<string, List<int>> series = [];
        foreach (UserActivityAggregate userAgg in perUser)
        {
            string label = names.TryGetValue(userAgg.UserId, out string? username)
                ? username
                : userAgg.UserId.ToString();

            List<int> daily = BuildDailyValues(userAgg.ByDay, start, days);
            series[label] = cumulative
                ? BuildCumulativeValues(daily, baselineMap.GetValueOrDefault(userAgg.UserId))
                : daily;
        }

        return series;
    }

    private async Task<Dictionary<DateTime, int>> GetGuildAggregateByDayAsync(DateTime start, int days, int guildId)
    {
        DateTime endExclusive = start.AddDays(days);
        return await dbContext.UserActivity
            .AsNoTracking()
            .Where(ua => ua.InsertDate >= start && ua.InsertDate < endExclusive && ua.GuildId == guildId)
            .GroupBy(ua => ua.InsertDate.Date)
            .Select(g => new { Day = g.Key, Xp = g.Sum(x => x.XpGained) })
            .ToDictionaryAsync(x => x.Day, x => (int)x.Xp);
    }

    private async Task<Dictionary<string, List<int>>> BuildGuildSeriesAsync(
        Dictionary<DateTime, int> dailyAggregate,
        DateTime start,
        int days,
        bool cumulative,
        int guildId)
    {
        if (dailyAggregate.Count == 0)
            return [];

        List<int> daily = BuildDailyValues(dailyAggregate, start, days);
        if (!cumulative)
            return new Dictionary<string, List<int>> { ["Guild Activity"] = daily };

        long baseline = await dbContext.UserActivity
            .AsNoTracking()
            .Where(ua => ua.InsertDate < start && ua.GuildId == guildId)
            .SumAsync(ua => (long?)ua.XpGained) ?? 0;

        return new Dictionary<string, List<int>> { ["Guild Activity"] = BuildCumulativeValues(daily, baseline) };
    }

    internal static List<int> BuildDailyValues(IReadOnlyDictionary<DateTime, int> valuesByDay, DateTime start, int days)
    {
        List<int> daily = new(new int[days]);
        for (int i = 0; i < days; i++)
        {
            DateTime day = start.AddDays(i);
            daily[i] = valuesByDay.GetValueOrDefault(day);
        }

        return daily;
    }

    internal static List<int> BuildCumulativeValues(IReadOnlyList<int> daily, long baseline)
    {
        int running = (int)baseline;
        List<int> cumulative = new(daily.Count);

        foreach (int value in daily)
        {
            running += value;
            cumulative.Add(running);
        }

        return cumulative;
    }
}

public sealed record ActivityGraphParseResult(bool Success, int Days, DateTime? ExplicitStart, string? ErrorMessage)
{
    public static ActivityGraphParseResult Valid(int days, DateTime? explicitStart) => new(true, days, explicitStart, null);

    public static ActivityGraphParseResult Error(string message) => new(false, 0, null, message);
}

public sealed record ActivityGraphRange(int Days, DateTime Start, DateTime? ExplicitStart)
{
    public DateTime End => (ExplicitStart ?? Start).AddDays(Days - 1).Date;
}

public sealed record ActivityGraphBuildResult(
    Dictionary<string, List<int>> Series,
    int Days,
    DateTime Start,
    string Message)
{
    public bool HasData => Series.Count > 0;
}

internal sealed record UserActivityAggregate(
    int UserId,
    int Total,
    IReadOnlyDictionary<DateTime, int> ByDay);
