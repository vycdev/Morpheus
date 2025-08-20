using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.Utilities.Images;
using System.Text;

namespace Morpheus.Modules;

public class LevelsModule(DB dbContext) : ModuleBase<SocketCommandContextExtended>
{
    [Name("Current Level")]
    [Summary("Displays the current level and experience points of the user.")]
    [Command("level")]
    [Alias("lvl", "currentlevel", "currentxp")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task CurrentLevelAsync()
    {
        User? user = Context.DbUser;
        Guild? guild = Context.DbGuild;

        if (user == null || guild == null)
        {
            await ReplyAsync("User or guild not found.");
            return;
        }

        IQueryable<UserLevels> userLevels = dbContext.UserLevels
            .Where(ul => ul.UserId == user.Id);

        UserLevels? userLevelGuild = userLevels
            .FirstOrDefault(ul => ul.GuildId == guild.Id);

        if (!userLevels.Any())
        {
            await ReplyAsync("There is no level information available for you in any guild.");
            return;
        }

        long totalXp = userLevels.Sum(ul => ul.TotalXp);
        int totalLevel = ActivityHandler.CalculateLevel(totalXp);
        long totalXpNeededForNextLevel = ActivityHandler.CalculateXp(totalLevel + 1);

        await ReplyAsync($"**Global**: Level **{totalLevel}** with **{totalXp}** XP");
        await ReplyAsync($"**{totalXpNeededForNextLevel - totalXp}** XP needed to level up globally \n");

        if (userLevelGuild != null)
        {
            await ReplyAsync($"**{guild.Name}**: Level **{userLevelGuild.Level}** with **{userLevelGuild.TotalXp}** XP");

            totalXpNeededForNextLevel = ActivityHandler.CalculateXp(userLevelGuild.Level + 1);

            await ReplyAsync($"**{totalXpNeededForNextLevel - userLevelGuild.TotalXp}** XP needed to level up");
            return;
        }
    }

    [Name("Activity Graph")]
    [Summary("Generates an activity graph for the top 10 users over the past n days.")]
    [Command("activitygraph")]
    [Alias("actgraph", "ag")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(2, 60)]
    public async Task ActivityGraphAsync(string days = "past7days", params IUser[] mentionedUsers)
    {
        var parse = await TryParseDaysStringAsync(days);
        if (!parse.success) return;
        int daysVal = parse.days;
        DateTime? explicitStart = parse.explicitStart;

        Guild? guild = Context.DbGuild;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        DateTime start = explicitStart ?? GetStartDate(daysVal);
        start = NormalizeToUtc(start);

        List<dynamic> perUser;
        if (mentionedUsers != null && mentionedUsers.Length > 0)
            perUser = GetTopUsersByWindowForMentions(start, daysVal, guildId: guild.Id, global: false, mentionedUsers);
        else
            perUser = GetTopUsersByWindow(start, daysVal, guildId: guild.Id, global: false);
        if (!perUser.Any())
        {
            await ReplyAsync("No activity data found for the requested period.");
            return;
        }

        var series = BuildSeries(perUser, start, daysVal, cumulative: false, global: false);
        string msg;
        if (explicitStart.HasValue)
        {
            var end = explicitStart.Value.AddDays(daysVal - 1).Date;
            msg = $"Top {series.Count} users activity from {explicitStart.Value:yyyy-MM-dd} to {end:yyyy-MM-dd} ({daysVal} days)";
        }
        else
        {
            msg = $"Top {series.Count} users activity for the last {daysVal} days";
        }
        await GenerateAndSendGraph(series, daysVal, "activity_graph.png", msg, start);
    }

    [Name("Activity Graph (Cumulative)")]
    [Summary("Generates a cumulative activity graph (running total) for the top 10 users over the past n days.")]
    [Command("activitygraphcumulative")]
    [Alias("actgraphcum", "agcum")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(2, 60)]
    public async Task ActivityGraphCumulativeAsync(string days = "past7days", params IUser[] mentionedUsers)
    {
        var parse = await TryParseDaysStringAsync(days);
        if (!parse.success) return;
        int daysVal = parse.days;
        DateTime? explicitStart = parse.explicitStart;

        Guild? guild = Context.DbGuild;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        DateTime start = explicitStart ?? GetStartDate(daysVal);
        start = NormalizeToUtc(start);

        List<dynamic> perUser;
        if (mentionedUsers != null && mentionedUsers.Length > 0)
            perUser = GetTopUsersByWindowForMentions(start, daysVal, guildId: guild.Id, global: false, mentionedUsers);
        else
            perUser = GetTopUsersByWindow(start, daysVal, guildId: guild.Id, global: false);
        if (!perUser.Any())
        {
            await ReplyAsync("No activity data found for the requested period.");
            return;
        }

        var series = BuildSeries(perUser, start, daysVal, cumulative: true, global: false);
        string msgCum;
        if (explicitStart.HasValue)
        {
            var end = explicitStart.Value.AddDays(daysVal - 1).Date;
            msgCum = $"Top {series.Count} users cumulative activity from {explicitStart.Value:yyyy-MM-dd} to {end:yyyy-MM-dd} ({daysVal} days)";
        }
        else
        {
            msgCum = $"Top {series.Count} users cumulative activity for the last {daysVal} days";
        }
        await GenerateAndSendGraph(series, daysVal, "activity_graph_cumulative.png", msgCum, start);
    }

    [Name("Global Activity Graph")]
    [Summary("Generates a global activity graph for the top 10 users across all guilds over the past n days.")]
    [Command("globalactivitygraph")]
    [Alias("globalactgraph", "gact")]
    [RateLimit(2, 60)]
    public async Task GlobalActivityGraphAsync(string days = "past7days", params IUser[] mentionedUsers)
    {
        var parse = await TryParseDaysStringAsync(days);
        if (!parse.success) return;
        int daysVal = parse.days;
        DateTime? explicitStart = parse.explicitStart;

        DateTime start = explicitStart ?? GetStartDate(daysVal);
        start = NormalizeToUtc(start);

        List<dynamic> perUser;
        if (mentionedUsers != null && mentionedUsers.Length > 0)
            perUser = GetTopUsersByWindowForMentions(start, daysVal, guildId: null, global: true, mentionedUsers);
        else
            perUser = GetTopUsersByWindow(start, daysVal, guildId: null, global: true);
        if (!perUser.Any())
        {
            await ReplyAsync("No activity data found for the requested period.");
            return;
        }

        var series = BuildSeries(perUser, start, daysVal, cumulative: false, global: true);
        string gmsg;
        if (explicitStart.HasValue)
        {
            var end = explicitStart.Value.AddDays(daysVal - 1).Date;
            gmsg = $"Top {series.Count} users global activity from {explicitStart.Value:yyyy-MM-dd} to {end:yyyy-MM-dd} ({daysVal} days)";
        }
        else
        {
            gmsg = $"Top {series.Count} users global activity for the last {daysVal} days";
        }
        await GenerateAndSendGraph(series, daysVal, "global_activity_graph.png", gmsg, start);
    }

    [Name("Global Activity Graph (Cumulative)")]
    [Summary("Generates a global cumulative activity graph (running total) for the top 10 users across all guilds over the past n days.")]
    [Command("globalactivitygraphcumulative")]
    [Alias("globalactgraphcum", "gactcum")]
    [RateLimit(2, 60)]
    public async Task GlobalActivityGraphCumulativeAsync(string days = "past7days", params IUser[] mentionedUsers)
    {
        var parse = await TryParseDaysStringAsync(days);
        if (!parse.success) return;
        int daysVal = parse.days;
        DateTime? explicitStart = parse.explicitStart;

        DateTime start = explicitStart ?? GetStartDate(daysVal);
        start = NormalizeToUtc(start);

        List<dynamic> perUser;
        if (mentionedUsers != null && mentionedUsers.Length > 0)
            perUser = GetTopUsersByWindowForMentions(start, daysVal, guildId: null, global: true, mentionedUsers);
        else
            perUser = GetTopUsersByWindow(start, daysVal, guildId: null, global: true);
        if (!perUser.Any())
        {
            await ReplyAsync("No activity data found for the requested period.");
            return;
        }

        var series = BuildSeries(perUser, start, daysVal, cumulative: true, global: true);
        string gmsgCum;
        if (explicitStart.HasValue)
        {
            var end = explicitStart.Value.AddDays(daysVal - 1).Date;
            gmsgCum = $"Top {series.Count} users global cumulative activity from {explicitStart.Value:yyyy-MM-dd} to {end:yyyy-MM-dd} ({daysVal} days)";
        }
        else
        {
            gmsgCum = $"Top {series.Count} users global cumulative activity for the last {daysVal} days";
        }
        await GenerateAndSendGraph(series, daysVal, "global_activity_graph_cumulative.png", gmsgCum, start);
    }

    [Name("Leaderboard")]
    [Summary("Displays the leaderboard of users in the guild based on their levels.")]
    [Command("leaderboard")]
    [Alias("lb", "top", "topusers")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task LeaderboardAsync(int page = 1)
    {
        Guild? guild = Context.DbGuild;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        IQueryable<UserLevels> userLevels = dbContext.UserLevels
            .Where(ul => ul.GuildId == guild.Id)
            .OrderByDescending(ul => ul.TotalXp)
            .Take(50);

        int totalUsers = userLevels.Count();
        int totalPages = (int)Math.Ceiling(totalUsers / (double)10);

        if (page < 1 || page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        IEnumerable<string> leaderboard = userLevels
            .Skip((page - 1) * 10)
            .Take(10)
            .Include(u => u.User)
            .ToList()
            .Select((ul, index) => $"[{((page - 1) * 10) + index + 1}] | {ul.User.Username}: Level {ActivityHandler.CalculateLevel(ul.TotalXp)} with {ul.TotalXp} XP");

        StringBuilder sb = new();

        sb.AppendLine($"**Leaderboard for {guild.Name}**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");

        await ReplyAsync(sb.ToString());
    }


    [Name("Leaderboard Past n Days")]
    [Summary("Displays the leaderboard of users in the guild based on their levels for the past n days.")]
    [Command("leaderboardpast")]
    [Alias("lbp", "toppast", "topuserpast")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 60)]
    public async Task LeaderboardPastAsync(int days, int page = 1)
    {
        Guild? guild = Context.DbGuild;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        if (days <= 0)
        {
            await ReplyAsync("Please provide a valid number of days greater than 0.");
            return;
        }

        IQueryable<UserLevels> userLevels = dbContext.UserActivity
            .Where(ua => ua.GuildId == guild.Id && ua.InsertDate >= DateTime.UtcNow.AddDays(-days))
            .GroupBy(ua => ua.User)
            .Select(g => new UserLevels
            {
                User = g.Key,
                GuildId = guild.Id,
                TotalXp = g.Sum(ua => ua.XpGained)
            })
            .OrderByDescending(ul => ul.TotalXp)
            .Take(50);

        int totalUsers = userLevels.Count();
        int totalPages = (int)Math.Ceiling(totalUsers / (double)10);

        if (page < 1 || page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        IEnumerable<string> leaderboard = userLevels
            .Skip((page - 1) * 10)
            .Take(10)
            .ToList()
            .Select((ul, index) => $"[{((page - 1) * 10) + index + 1}] | {ul.User.Username}: Level {ActivityHandler.CalculateLevel(ul.TotalXp)} with {ul.TotalXp} XP");

        StringBuilder sb = new();

        sb.AppendLine($"**Leaderboard for {guild.Name}** for the past **{days}** days");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");

        await ReplyAsync(sb.ToString());
    }

    [Name("Global Leaderboard")]
    [Summary("Displays the global leaderboard of users based on their levels across all guilds.")]
    [Command("globalleaderboard")]
    [Alias("globallb", "globaltop", "globaltopusers")]
    [RateLimit(3, 10)]
    public async Task GlobalLeaderboardAsync(int page = 1)
    {
        IQueryable<UserLevels> userLevels = dbContext.UserLevels
            .GroupBy(ul => ul.User)
            .Select(g => new UserLevels
            {
                User = g.Key,
                TotalXp = g.Sum(ul => ul.TotalXp)
            })
            .OrderByDescending(ul => ul.TotalXp)
            .Take(50);

        int totalUsers = userLevels.Count();
        int totalPages = (int)Math.Ceiling(totalUsers / (double)10);

        if (page < 1 || page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        IEnumerable<string> leaderboard = userLevels
            .Skip((page - 1) * 10)
            .Take(10)
            .ToList()
            .Select((ul, index) => $"[{((page - 1) * 10) + index + 1}] | {ul.User.Username}: Level {ActivityHandler.CalculateLevel(ul.TotalXp)} with {ul.TotalXp} XP");

        StringBuilder sb = new();

        sb.AppendLine("**Global Leaderboard**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");

        await ReplyAsync(sb.ToString());
    }

    [Name("Global Leaderboard Past n Days")]
    [Summary("Displays the global leaderboard of users based on their levels across all guilds for the past n days.")]
    [Command("globalleaderboardpast")]
    [Alias("globallbp", "globaltoppast", "globaltopuserspast")]
    [RateLimit(3, 60)]
    public async Task GlobalLeaderboardPastAsync(int days, int page = 1)
    {
        if (days <= 0)
        {
            await ReplyAsync("Please provide a valid number of days greater than 0.");
            return;
        }

        IQueryable<UserLevels> userLevels = dbContext.UserActivity
            .Where(ua => ua.InsertDate >= DateTime.UtcNow.AddDays(-days))
            .GroupBy(ua => ua.User)
            .Select(g => new UserLevels
            {
                User = g.Key,
                TotalXp = g.Sum(ua => ua.XpGained)
            })
            .OrderByDescending(ul => ul.TotalXp)
            .Take(50);

        int totalUsers = userLevels.Count();
        int totalPages = (int)Math.Ceiling(totalUsers / (double)10);

        if (page < 1 || page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        IEnumerable<string> leaderboard = userLevels
            .Skip((page - 1) * 10)
            .Take(10)
            .ToList()
            .Select((ul, index) => $"[{((page - 1) * 10) + index + 1}] | {ul.User.Username}: Level {ActivityHandler.CalculateLevel(ul.TotalXp)} with {ul.TotalXp} XP");

        StringBuilder sb = new();
        sb.AppendLine($"**Global Leaderboard** for the past **{days}** days");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");
        await ReplyAsync(sb.ToString());
    }

    // ---------------------- Helper methods to reduce duplication ----------------------

    private bool ValidateDays(int days)
    {
        if (days <= 0 || days > 90)
        {
            ReplyAsync("Please provide a number of days between 1 and 90.").GetAwaiter().GetResult();
            return false;
        }
        return true;
    }

    private DateTime GetStartDate(int days) => DateTime.UtcNow.Date.AddDays(-(days - 1));

    private DateTime NormalizeToUtc(DateTime dt) => DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    // Parse days parameter which supports:
    // - preset tokens: past7days, past30days, past60days, past90days
    // - explicit integer string: "7", "30"
    // - date range like "2025-01-01..2025-01-31"
    // Returns parsed days (int) and optional explicit start date (if a range was provided).
    private Task<(bool success, int days, DateTime? explicitStart)> TryParseDaysStringAsync(string input)
    {
        return Task.Run(async () =>
        {
            if (string.IsNullOrWhiteSpace(input)) input = "past7days";

            input = input.Trim().ToLowerInvariant();

            if (input.StartsWith("past") && input.EndsWith("days"))
            {
                var num = input.Substring(4, input.Length - 8);
                if (int.TryParse(num, out int parsed))
                {
                    if (parsed < 7) parsed = 7;
                    if (parsed > 90) parsed = 90;
                    return (true, parsed, (DateTime?)null);
                }
                await ReplyAsync("Please provide a number of days between 7 and 90 or a valid preset (past7days, past30days, past60days, past90days).\nOr provide a date range like 2025-01-01..2025-01-31.");
                return (false, 0, null);
            }

            if (int.TryParse(input, out int asInt))
            {
                if (asInt < 7) asInt = 7;
                if (asInt > 90) asInt = 90;
                return (true, asInt, null);
            }

            if (input.Contains(".."))
            {
                var parts = input.Split(new[] { ".." }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2
                    && DateTime.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start)
                    && DateTime.TryParseExact(parts[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var end))
                {
                    start = start.Date;
                    end = end.Date;
                    // If end < start, swap them
                    if (end < start)
                    {
                        var tmp = start; start = end; end = tmp;
                    }
                    var span = (end - start).TotalDays + 1;
                    // enforce minimum 7 days
                    if (span < 7)
                    {
                        end = start.AddDays(6); // inclusive 7 days
                        span = 7;
                    }
                    if (span > 90)
                    {
                        await ReplyAsync("Date range exceeds maximum of 90 days.");
                        return (false, 0, null);
                    }
                    return (true, (int)span, DateTime.SpecifyKind(start, DateTimeKind.Utc));
                }
                await ReplyAsync("Invalid date range format. Use YYYY-MM-DD..YYYY-MM-DD and ensure the range is at most 90 days and start <= end.");
                return (false, 0, null);
            }

            await ReplyAsync("Unrecognized days parameter. Use presets (past7days) or integer days or a date range (YYYY-MM-DD..YYYY-MM-DD). Default is past7days.");
            return (false, 0, null);
        });
    }

    // Returns an anonymous-type-like list of per-user aggregates: UserId, Total, ByDay
    private List<dynamic> GetTopUsersByWindow(DateTime start, int days, int? guildId, bool global)
    {
        start = NormalizeToUtc(start);
        var query = dbContext.UserActivity.AsQueryable();
        if (!global && guildId.HasValue)
            query = query.Where(ua => ua.GuildId == guildId.Value);

        var q = query
            .Where(ua => ua.InsertDate >= start)
            .AsEnumerable()
            .GroupBy(ua => new { ua.UserId, Day = ua.InsertDate.Date })
            .Select(g => new
            {
                UserId = g.Key.UserId,
                Day = g.Key.Day,
                Xp = g.Sum(x => x.XpGained)
            })
            .ToList();

        var perUser = q.GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Total = g.Sum(x => x.Xp),
                ByDay = g.ToDictionary(x => x.Day, x => x.Xp)
            })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToList<dynamic>();

        return perUser;
    }

    private Dictionary<string, List<int>> BuildSeries(List<dynamic> perUser, DateTime start, int days, bool cumulative, bool global)
    {
        var series = new Dictionary<string, List<int>>();

        // If cumulative, compute baseline per user (activity before start)
        Dictionary<int, long> baselineMap = new();
        if (cumulative)
        {
            var userIds = perUser.Select(p => (int)p.UserId).ToList();
            var baseQuery = dbContext.UserActivity.AsQueryable()
                .Where(ua => ua.InsertDate < start && userIds.Contains(ua.UserId));
            // if per-guild baseline is required, caller should have filtered guild in perUser selection
            var baseList = baseQuery
                .GroupBy(ua => ua.UserId)
                .Select(g => new { UserId = g.Key, Baseline = (long)g.Sum(x => x.XpGained) })
                .ToList();
            baselineMap = baseList.ToDictionary(x => x.UserId, x => x.Baseline);
        }

        foreach (var userAgg in perUser)
        {
            int userId = (int)userAgg.UserId;
            var dbUser = dbContext.Users.Find(userId);
            string label = dbUser != null ? dbUser.Username : userId.ToString();

            List<int> daily = new List<int>(new int[days]);
            for (int i = 0; i < days; i++)
            {
                DateTime day = start.AddDays(i);
                if (((IDictionary<DateTime, int>)userAgg.ByDay).TryGetValue(day, out int xp)) daily[i] = xp;
                else daily[i] = 0;
            }

            if (cumulative)
            {
                int running = 0;
                if (baselineMap.TryGetValue(userId, out var b)) running = (int)b;
                List<int> cumulativeList = new List<int>(days);
                for (int i = 0; i < days; i++)
                {
                    running += daily[i];
                    cumulativeList.Add(running);
                }
                series[label] = cumulativeList;
            }
            else
            {
                series[label] = daily;
            }
        }

        return series;
    }

    // Convert each series to a 7-day rolling average (rounded to nearest int)
    private Dictionary<string, List<int>> SevenDayRollingAverage(Dictionary<string, List<int>> series)
    {
        var outSeries = new Dictionary<string, List<int>>();
        foreach (var kv in series)
        {
            var vals = kv.Value;
            var n = vals.Count;
            var avg = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                int start = Math.Max(0, i - 6);
                int len = i - start + 1;
                double sum = 0;
                for (int j = start; j <= i; j++) sum += vals[j];
                avg.Add((int)Math.Round(sum / len));
            }
            outSeries[kv.Key] = avg;
        }
        return outSeries;
    }

    [Name("Activity Graph (7-day roll)")]
    [Summary("Generates a 7-day rolling average activity graph for the top 10 users over the past n days.")]
    [Command("activitygraph7day")]
    [Alias("actgraph7", "ag7")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(2, 60)]
    public async Task ActivityGraph7DayAsync(string days = "past7days", params IUser[] mentionedUsers)
    {
        var parse = await TryParseDaysStringAsync(days);
        if (!parse.success) return;
        int daysVal = parse.days;
        DateTime? explicitStart = parse.explicitStart;

        Guild? guild = Context.DbGuild;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        DateTime start = explicitStart ?? GetStartDate(daysVal);
        start = NormalizeToUtc(start);

        List<dynamic> perUser;
        if (mentionedUsers != null && mentionedUsers.Length > 0)
            perUser = GetTopUsersByWindowForMentions(start, daysVal, guildId: guild.Id, global: false, mentionedUsers);
        else
            perUser = GetTopUsersByWindow(start, daysVal, guildId: guild.Id, global: false);
        if (!perUser.Any())
        {
            await ReplyAsync("No activity data found for the requested period.");
            return;
        }

        var series = BuildSeries(perUser, start, daysVal, cumulative: false, global: false);
        var rolling = SevenDayRollingAverage(series);
        string msg7;
        if (explicitStart.HasValue)
        {
            var end = explicitStart.Value.AddDays(daysVal - 1).Date;
            msg7 = $"Top {rolling.Count} users 7-day rolling average activity from {explicitStart.Value:yyyy-MM-dd} to {end:yyyy-MM-dd} ({daysVal} days)";
        }
        else
        {
            msg7 = $"Top {rolling.Count} users 7-day rolling average activity for the last {daysVal} days";
        }
        await GenerateAndSendGraph(rolling, daysVal, "activity_graph_7day.png", msg7, start);
    }

    [Name("Global Activity Graph (7-day roll)")]
    [Summary("Generates a global 7-day rolling average activity graph for the top 10 users over the past n days.")]
    [Command("globalactivitygraph7day")]
    [Alias("globalactgraph7", "gact7")]
    [RateLimit(2, 60)]
    public async Task GlobalActivityGraph7DayAsync(string days = "past7days", params IUser[] mentionedUsers)
    {
        var parse = await TryParseDaysStringAsync(days);
        if (!parse.success) return;
        int daysVal = parse.days;
        DateTime? explicitStart = parse.explicitStart;

        DateTime start = explicitStart ?? GetStartDate(daysVal);

        List<dynamic> perUser;
        if (mentionedUsers != null && mentionedUsers.Length > 0)
            perUser = GetTopUsersByWindowForMentions(start, daysVal, guildId: null, global: true, mentionedUsers);
        else
            perUser = GetTopUsersByWindow(start, daysVal, guildId: null, global: true);
        if (!perUser.Any())
        {
            await ReplyAsync("No activity data found for the requested period.");
            return;
        }

        var series = BuildSeries(perUser, start, daysVal, cumulative: false, global: true);
        var rolling = SevenDayRollingAverage(series);
        string gmsg7;
        if (explicitStart.HasValue)
        {
            var end = explicitStart.Value.AddDays(daysVal - 1).Date;
            gmsg7 = $"Top {rolling.Count} users global 7-day rolling average activity from {explicitStart.Value:yyyy-MM-dd} to {end:yyyy-MM-dd} ({daysVal} days)";
        }
        else
        {
            gmsg7 = $"Top {rolling.Count} users global 7-day rolling average activity for the last {daysVal} days";
        }
        await GenerateAndSendGraph(rolling, daysVal, "global_activity_graph_7day.png", gmsg7, start);
    }

    private async Task GenerateAndSendGraph(Dictionary<string, List<int>> series, int days, string filename, string message, DateTime? start = null)
    {
        byte[] png;
        try
        {
            png = ActivityGraphGenerator.GenerateLineChart(series, days, start: start);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GRAPH ERROR] {ex}");
            await ReplyAsync("Failed to generate activity graph. Try again later.");
            return;
        }

        using var ms = new MemoryStream(png);
        await Context.Channel.SendFileAsync(ms, filename, message);
    }

    // Build the per-user aggregate list based on explicitly mentioned Discord users.
    // This returns the same anonymous-typed shape as GetTopUsersByWindow: { UserId, Total, ByDay }
    private List<dynamic> GetTopUsersByWindowForMentions(DateTime start, int days, int? guildId, bool global, IUser[] mentionedUsers)
    {
        start = NormalizeToUtc(start);
        var list = new List<dynamic>();

        foreach (var mu in mentionedUsers)
        {
            if (mu == null) continue;
            // Find DB user by DiscordId
            var dbUser = dbContext.Users.FirstOrDefault(u => u.DiscordId == mu.Id);
            if (dbUser == null) continue;

            int userId = dbUser.Id;

            var activityQuery = dbContext.UserActivity.AsQueryable().Where(ua => ua.UserId == userId);
            if (!global && guildId.HasValue)
                activityQuery = activityQuery.Where(ua => ua.GuildId == guildId.Value);

            var byDayList = activityQuery
                .Where(ua => ua.InsertDate >= start)
                .AsEnumerable()
                .GroupBy(ua => ua.InsertDate.Date)
                .Select(g => new { Day = g.Key, Xp = g.Sum(x => x.XpGained) })
                .ToList();

            var byDayDict = byDayList.ToDictionary(x => x.Day, x => (int)x.Xp);
            int total = byDayList.Sum(x => (int)x.Xp);

            // Ensure we include users even if they have no activity in the window
            list.Add(new { UserId = userId, Total = total, ByDay = (IDictionary<DateTime, int>)byDayDict });
        }

        return list;
    }
}
