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
using Morpheus.Utilities;

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
    [RequireBotPermission(GuildPermission.AttachFiles)]
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

        var series = BuildSeries(perUser, start, daysVal, cumulative: false, global: false, guildId: guild.Id);
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
    [RequireBotPermission(GuildPermission.AttachFiles)]
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

        var series = BuildSeries(perUser, start, daysVal, cumulative: true, global: false, guildId: guild.Id);
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
    [RequireBotPermission(GuildPermission.AttachFiles)]
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

        var series = BuildSeries(perUser, start, daysVal, cumulative: false, global: true, guildId: null);
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
    [RequireBotPermission(GuildPermission.AttachFiles)]
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

        var series = BuildSeries(perUser, start, daysVal, cumulative: true, global: true, guildId: null);
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
        var me = Context.DbUser;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        IQueryable<UserLevels> userLevels = dbContext.UserLevels
            .Where(ul => ul.GuildId == guild.Id)
            .OrderByDescending(ul => ul.TotalXp);

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
            .Select((ul, index) =>
            {
                string name = ul.User?.Username ?? ul.UserId.ToString();
                return $"[{((page - 1) * 10) + index + 1}] | {name}: Level {ActivityHandler.CalculateLevel(ul.TotalXp)} with {ul.TotalXp} XP";
            });

        StringBuilder sb = new();

        sb.AppendLine($"**Leaderboard for {guild.Name}**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");

        // User rank for this guild by total XP (all time)
        string rankLine = "Your rank: N/A";
        if (me != null)
        {
            var myUl = dbContext.UserLevels.AsNoTracking()
                .FirstOrDefault(ul => ul.GuildId == guild.Id && ul.UserId == me.Id);
            if (myUl != null)
            {
                int better = dbContext.UserLevels.AsNoTracking()
                    .Where(ul => ul.GuildId == guild.Id && ul.TotalXp > myUl.TotalXp)
                    .Count();
                rankLine = $"Your rank: #{better + 1}";
            }
        }
        sb.AppendLine(rankLine);

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
        var me = Context.DbUser;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        int maxDaysLb = Env.Get<int>("ACTIVITY_GRAPHS_MAX_DAYS", 90);
        if (days <= 0)
        {
            await ReplyAsync("Please provide a valid number of days greater than 0.");
            return;
        }
        if (days > maxDaysLb)
        {
            await ReplyAsync($"Capping to maximum of {maxDaysLb} days.");
            days = maxDaysLb;
        }

        var cutoff = DateTime.UtcNow.AddDays(-days);
        var baseQuery = dbContext.UserActivity.AsNoTracking()
            .Where(ua => ua.GuildId == guild.Id && ua.InsertDate >= cutoff);

        var top50 = baseQuery
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, TotalXp = g.Sum(x => x.XpGained) })
            .OrderByDescending(x => x.TotalXp);

        int totalUsers = top50.Count();
        int totalPages = (int)Math.Ceiling(totalUsers / 10.0);
        if (page < 1 || page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        var pageItems = top50
            .Skip((page - 1) * 10)
            .Take(10)
            .ToList();

        var userIds = pageItems.Select(x => x.UserId).ToList();
        var names = dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username })
            .ToList()
            .ToDictionary(u => u.Id, u => u.Username);

        IEnumerable<string> leaderboard = pageItems
            .Select((x, index) =>
            {
                string name = names.TryGetValue(x.UserId, out var n) ? n : x.UserId.ToString();
                return $"[{((page - 1) * 10) + index + 1}] | {name}: Level {ActivityHandler.CalculateLevel(x.TotalXp)} with {x.TotalXp} XP";
            });

        StringBuilder sb = new();

        sb.AppendLine($"**Leaderboard for {guild.Name}** for the past **{days}** days");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");

        // User rank for this guild by XP in past N days
        string rankLine = "Your rank: N/A";
        if (me != null)
        {
            bool hasUser = baseQuery.Any(ua => ua.UserId == me.Id);
            if (hasUser)
            {
                int mySum = baseQuery.Where(ua => ua.UserId == me.Id).Sum(x => x.XpGained);
                int better = baseQuery
                    .GroupBy(ua => ua.UserId)
                    .Select(g => new { Sum = g.Sum(x => x.XpGained) })
                    .Count(x => x.Sum > mySum);
                rankLine = $"Your rank: #{better + 1}";
            }
        }
        sb.AppendLine(rankLine);

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
            .OrderByDescending(ul => ul.TotalXp);

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
            .Select((ul, index) =>
            {
                string name = ul.User?.Username ?? ul.UserId.ToString();
                return $"[{((page - 1) * 10) + index + 1}] | {name}: Level {ActivityHandler.CalculateLevel(ul.TotalXp)} with {ul.TotalXp} XP";
            });

        StringBuilder sb = new();

        sb.AppendLine("**Global Leaderboard**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");

        // User global rank by total XP (all time)
        string rankLine = "Your rank: N/A";
        var me = Context.DbUser;
        if (me != null)
        {
            bool anyRows = dbContext.UserLevels.AsNoTracking().Any(ul => ul.UserId == me.Id);
            if (anyRows)
            {
                long myTotal = dbContext.UserLevels.AsNoTracking()
                    .Where(ul => ul.UserId == me.Id)
                    .Select(ul => (long)ul.TotalXp)
                    .Sum();
                int better = dbContext.UserLevels.AsNoTracking()
                    .GroupBy(ul => ul.UserId)
                    .Select(g => new { Total = g.Sum(ul => ul.TotalXp) })
                    .Count(x => x.Total > myTotal);
                rankLine = $"Your rank: #{better + 1}";
            }
        }
        sb.AppendLine(rankLine);

        await ReplyAsync(sb.ToString());
    }

    [Name("Global Leaderboard Past n Days")]
    [Summary("Displays the global leaderboard of users based on their levels across all guilds for the past n days.")]
    [Command("globalleaderboardpast")]
    [Alias("globallbp", "globaltoppast", "globaltopuserspast")]
    [RateLimit(3, 60)]
    public async Task GlobalLeaderboardPastAsync(int days, int page = 1)
    {
        int maxDaysGlb = Env.Get<int>("ACTIVITY_GRAPHS_MAX_DAYS", 90);
        if (days <= 0)
        {
            await ReplyAsync("Please provide a valid number of days greater than 0.");
            return;
        }
        if (days > maxDaysGlb)
        {
            await ReplyAsync($"Capping to maximum of {maxDaysGlb} days.");
            days = maxDaysGlb;
        }

        var cutoff = DateTime.UtcNow.AddDays(-days);
        var baseQuery = dbContext.UserActivity.AsNoTracking()
            .Where(ua => ua.InsertDate >= cutoff);

        var top50 = baseQuery
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, TotalXp = g.Sum(x => x.XpGained) })
            .OrderByDescending(x => x.TotalXp);

        int totalUsers = top50.Count();
        int totalPages = (int)Math.Ceiling(totalUsers / 10.0);
        if (page < 1 || page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        var pageItems = top50
            .Skip((page - 1) * 10)
            .Take(10)
            .ToList();

        var userIds = pageItems.Select(x => x.UserId).ToList();
        var names = dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username })
            .ToList()
            .ToDictionary(u => u.Id, u => u.Username);

        IEnumerable<string> leaderboard = pageItems
            .Select((x, index) =>
            {
                string name = names.TryGetValue(x.UserId, out var n) ? n : x.UserId.ToString();
                return $"[{((page - 1) * 10) + index + 1}] | {name}: Level {ActivityHandler.CalculateLevel(x.TotalXp)} with {x.TotalXp} XP";
            });

        StringBuilder sb = new();
        sb.AppendLine($"**Global Leaderboard** for the past **{days}** days");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");
        // User global rank by XP in past N days
        string rankLine = "Your rank: N/A";
        var me = Context.DbUser;
        if (me != null)
        {
            bool hasUser = baseQuery.Any(ua => ua.UserId == me.Id);
            if (hasUser)
            {
                int mySum = baseQuery.Where(ua => ua.UserId == me.Id).Sum(x => x.XpGained);
                int better = baseQuery
                    .GroupBy(ua => ua.UserId)
                    .Select(g => new { Sum = g.Sum(x => x.XpGained) })
                    .Count(x => x.Sum > mySum);
                rankLine = $"Your rank: #{better + 1}";
            }
        }
        sb.AppendLine(rankLine);
        await ReplyAsync(sb.ToString());
    }

    [Name("Leaderboard Messages")]
    [Summary("Displays the leaderboard by number of messages sent in this guild (all time).")]
    [Command("leaderboardmessages")]
    [Alias("lbm", "topmessages", "messageslb", "msgslb")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task LeaderboardMessagesAsync(int page = 1)
    {
        Guild? guild = Context.DbGuild;
        var me = Context.DbUser;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        var query = dbContext.UserLevels
            .Where(ul => ul.GuildId == guild.Id && ul.UserMessageCount > 0)
            .OrderByDescending(ul => ul.UserMessageCount);

        int totalUsers = query.Count();
        if (totalUsers == 0)
        {
            await ReplyAsync("No message data found for this guild.");
            return;
        }
        int totalPages = (int)Math.Ceiling(totalUsers / 10.0);
        if (page < 1 || page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        var pageItems = query
            .Skip((page - 1) * 10)
            .Take(10)
            .Include(ul => ul.User)
            .ToList();

        var lines = pageItems.Select((ul, index) =>
        {
            string name = ul.User?.Username ?? ul.UserId.ToString();
            return $"[{((page - 1) * 10) + index + 1}] | {name}: Messages {ul.UserMessageCount}";
        });

        StringBuilder sb = new();
        sb.AppendLine($"**Messages Leaderboard for {guild.Name}** (all time)");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", lines));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");
        // User rank by messages (guild all time)
        string rankLine = "Your rank: N/A";
        if (me != null)
        {
            var myUl = dbContext.UserLevels.AsNoTracking().FirstOrDefault(ul => ul.GuildId == guild.Id && ul.UserId == me.Id);
            if (myUl != null)
            {
                int better = dbContext.UserLevels.AsNoTracking()
                    .Where(ul => ul.GuildId == guild.Id && ul.UserMessageCount > myUl.UserMessageCount)
                    .Count();
                rankLine = $"Your rank: #{better + 1}";
            }
        }
        sb.AppendLine(rankLine);
        await ReplyAsync(sb.ToString());
    }

    [Name("Leaderboard Messages Past n Days")]
    [Summary("Displays the leaderboard by number of messages sent in this guild for the past n days.")]
    [Command("leaderboardmessagespast")]
    [Alias("lbmp", "topmessagespast", "messageslbpast")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 60)]
    public async Task LeaderboardMessagesPastAsync(int days, int page = 1)
    {
        Guild? guild = Context.DbGuild;
        var me = Context.DbUser;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        int maxDays = Env.Get<int>("ACTIVITY_GRAPHS_MAX_DAYS", 90);
        if (days <= 0)
        {
            await ReplyAsync("Please provide a valid number of days greater than 0.");
            return;
        }
        if (days > maxDays)
        {
            await ReplyAsync($"Capping to maximum of {maxDays} days.");
            days = maxDays;
        }

        var cutoff = DateTime.UtcNow.AddDays(-days);
        var baseQuery = dbContext.UserActivity.AsNoTracking()
            .Where(ua => ua.GuildId == guild.Id && ua.InsertDate >= cutoff);

        var top50 = baseQuery
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count);

        int totalUsers = top50.Count();
        if (totalUsers == 0)
        {
            await ReplyAsync($"No message data found for the past {days} days.");
            return;
        }
        int totalPages = (int)Math.Ceiling(totalUsers / 10.0);
        if (page < 1 || page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        var pageItems = top50
            .Skip((page - 1) * 10)
            .Take(10)
            .ToList();

        var userIds = pageItems.Select(x => x.UserId).ToList();
        var names = dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username })
            .ToList()
            .ToDictionary(u => u.Id, u => u.Username);

        var lines = pageItems.Select((x, index) =>
        {
            string name = names.TryGetValue(x.UserId, out var n) ? n : x.UserId.ToString();
            return $"[{((page - 1) * 10) + index + 1}] | {name}: Messages {x.Count}";
        });

        StringBuilder sb = new();
        sb.AppendLine($"**Messages Leaderboard for {guild.Name}** for the past **{days}** days");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", lines));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");
        // User rank by messages (guild past N days)
        string rankLine = "Your rank: N/A";
        if (me != null)
        {
            bool hasUser = baseQuery.Any(ua => ua.UserId == me.Id);
            if (hasUser)
            {
                int myCount = baseQuery.Count(ua => ua.UserId == me.Id);
                int better = baseQuery
                    .GroupBy(ua => ua.UserId)
                    .Select(g => new { C = g.Count() })
                    .Count(x => x.C > myCount);
                rankLine = $"Your rank: #{better + 1}";
            }
        }
        sb.AppendLine(rankLine);
        await ReplyAsync(sb.ToString());
    }

    [Name("Global Leaderboard Messages")]
    [Summary("Displays the global leaderboard by number of messages sent across all guilds (all time).")]
    [Command("globalleaderboardmessages")]
    [Alias("globallbm", "globaltopmessages", "globalmessageslb")]
    [RateLimit(3, 10)]
    public async Task GlobalLeaderboardMessagesAsync(int page = 1)
    {
        var top50 = dbContext.UserLevels.AsNoTracking()
                .GroupBy(ul => ul.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Sum(ul => ul.UserMessageCount) })
                .OrderByDescending(x => x.Count);

        int totalUsers = top50.Count();
        if (totalUsers == 0)
        {
            await ReplyAsync("No message data found globally.");
            return;
        }
        int totalPages = (int)Math.Ceiling(totalUsers / 10.0);
        if (page < 1 || page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        var pageItems = top50
            .Skip((page - 1) * 10)
            .Take(10)
            .ToList();

        var userIds = pageItems.Select(x => x.UserId).ToList();
        var names = dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username })
            .ToList()
            .ToDictionary(u => u.Id, u => u.Username);

        var lines = pageItems.Select((x, index) =>
        {
            string name = names.TryGetValue(x.UserId, out var n) ? n : x.UserId.ToString();
            return $"[{((page - 1) * 10) + index + 1}] | {name}: Messages {x.Count}";
        });

        StringBuilder sb = new();
        sb.AppendLine("**Global Messages Leaderboard** (all time)");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", lines));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");
        // User global rank by messages (all time)
        string rankLine = "Your rank: N/A";
        var me = Context.DbUser;
        if (me != null)
        {
            bool anyRows = dbContext.UserLevels.AsNoTracking().Any(ul => ul.UserId == me.Id);
            if (anyRows)
            {
                long myCount = dbContext.UserLevels.AsNoTracking()
                    .Where(ul => ul.UserId == me.Id)
                    .Select(ul => (long)ul.UserMessageCount)
                    .Sum();
                int better = dbContext.UserLevels.AsNoTracking()
                    .GroupBy(ul => ul.UserId)
                    .Select(g => new { C = g.Sum(ul => ul.UserMessageCount) })
                    .Count(x => x.C > myCount);
                rankLine = $"Your rank: #{better + 1}";
            }
        }
        sb.AppendLine(rankLine);
        await ReplyAsync(sb.ToString());
    }

    [Name("Global Leaderboard Messages Past n Days")]
    [Summary("Displays the global leaderboard by number of messages sent across all guilds for the past n days.")]
    [Command("globalleaderboardmessagespast")]
    [Alias("globallbmp", "globaltopmessagespast", "globalmessageslbpast")]
    [RateLimit(3, 60)]
    public async Task GlobalLeaderboardMessagesPastAsync(int days, int page = 1)
    {
        int maxDays = Env.Get<int>("ACTIVITY_GRAPHS_MAX_DAYS", 90);
        if (days <= 0)
        {
            await ReplyAsync("Please provide a valid number of days greater than 0.");
            return;
        }
        if (days > maxDays)
        {
            await ReplyAsync($"Capping to maximum of {maxDays} days.");
            days = maxDays;
        }

        var cutoff = DateTime.UtcNow.AddDays(-days);
        var baseQuery = dbContext.UserActivity.AsNoTracking()
            .Where(ua => ua.InsertDate >= cutoff);

        var top50 = baseQuery
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count);

        int totalUsers = top50.Count();
        if (totalUsers == 0)
        {
            await ReplyAsync($"No message data found globally for the past {days} days.");
            return;
        }
        int totalPages = (int)Math.Ceiling(totalUsers / 10.0);
        if (page < 1 || page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        var pageItems = top50
            .Skip((page - 1) * 10)
            .Take(10)
            .ToList();

        var userIds = pageItems.Select(x => x.UserId).ToList();
        var names = dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username })
            .ToList()
            .ToDictionary(u => u.Id, u => u.Username);

        var lines = pageItems.Select((x, index) =>
        {
            string name = names.TryGetValue(x.UserId, out var n) ? n : x.UserId.ToString();
            return $"[{((page - 1) * 10) + index + 1}] | {name}: Messages {x.Count}";
        });

        StringBuilder sb = new();
        sb.AppendLine($"**Global Messages Leaderboard** for the past **{days}** days");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", lines));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");
        // User global rank by messages (past N days)
        string rankLine = "Your rank: N/A";
        var me = Context.DbUser;
        if (me != null)
        {
            bool hasUser = baseQuery.Any(ua => ua.UserId == me.Id);
            if (hasUser)
            {
                int myCount = baseQuery.Count(ua => ua.UserId == me.Id);
                int better = baseQuery
                    .GroupBy(ua => ua.UserId)
                    .Select(g => new { C = g.Count() })
                    .Count(x => x.C > myCount);
                rankLine = $"Your rank: #{better + 1}";
            }
        }
        sb.AppendLine(rankLine);
        await ReplyAsync(sb.ToString());
    }

    [Name("Leaderboard Avg Message Length")]
    [Summary("Displays the leaderboard by average message length in this guild (all time).")]
    [Command("leaderboardavglength")]
    [Alias("lbal", "topavglength", "avglenlb")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task LeaderboardAvgLengthAsync(int page = 1)
    {
        Guild? guild = Context.DbGuild;
        var me = Context.DbUser;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        var query = dbContext.UserLevels
            .Where(ul => ul.GuildId == guild.Id && ul.UserMessageCount > 0)
            .OrderByDescending(ul => ul.UserAverageMessageLength);

        int totalUsers = query.Count();
        if (totalUsers == 0)
        {
            await ReplyAsync("No message data found for this guild.");
            return;
        }
        int totalPages = (int)Math.Ceiling(totalUsers / 10.0);
        if (page < 1 || page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        var pageItems = query
            .Skip((page - 1) * 10)
            .Take(10)
            .Include(ul => ul.User)
            .ToList();

        var lines = pageItems.Select((ul, index) =>
        {
            string name = ul.User?.Username ?? ul.UserId.ToString();
            string avg = ul.UserAverageMessageLength.ToString("0.0");
            return $"[{((page - 1) * 10) + index + 1}] | {name}: Avg length {avg} chars ({ul.UserMessageCount} msgs)";
        });

        StringBuilder sb = new();
        sb.AppendLine($"**Average Message Length Leaderboard for {guild.Name}** (all time)");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", lines));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");
        // User rank by average message length (guild all time)
        string rankLine = "Your rank: N/A";
        if (me != null)
        {
            var myUl = dbContext.UserLevels.AsNoTracking()
                .FirstOrDefault(ul => ul.GuildId == guild.Id && ul.UserId == me.Id && ul.UserMessageCount > 0);
            if (myUl != null)
            {
                double myAvg = myUl.UserAverageMessageLength;
                int better = dbContext.UserLevels.AsNoTracking()
                    .Where(ul => ul.GuildId == guild.Id && ul.UserMessageCount > 0 && ul.UserAverageMessageLength > myAvg)
                    .Count();
                rankLine = $"Your rank: #{better + 1}";
            }
        }
        sb.AppendLine(rankLine);
        await ReplyAsync(sb.ToString());
    }

    [Name("Global Leaderboard Avg Message Length")]
    [Summary("Displays the global leaderboard by average message length across all guilds (all time). Weighted by message count.")]
    [Command("globalleaderboardavglength")]
    [Alias("globallbal", "globaltopavglength", "globalavglenlb")]
    [RateLimit(3, 10)]
    public async Task GlobalLeaderboardAvgLengthAsync(int page = 1)
    {
        var top50 = dbContext.UserLevels.AsNoTracking()
                .GroupBy(ul => ul.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    SumLen = g.Sum(ul => ul.UserAverageMessageLength * ul.UserMessageCount),
                    SumCount = g.Sum(ul => ul.UserMessageCount)
                })
                .Where(x => x.SumCount > 0)
                .Select(x => new { x.UserId, AvgLen = x.SumLen / x.SumCount })
                .OrderByDescending(x => x.AvgLen);

        int totalUsers = top50.Count();
        if (totalUsers == 0)
        {
            await ReplyAsync("No message data found globally.");
            return;
        }
        int totalPages = (int)Math.Ceiling(totalUsers / 10.0);
        if (page < 1 || page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        var pageItems = top50
            .Skip((page - 1) * 10)
            .Take(10)
            .ToList();

        var userIds = pageItems.Select(x => x.UserId).ToList();
        var names = dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username })
            .ToList()
            .ToDictionary(u => u.Id, u => u.Username);

        var lines = pageItems.Select((x, index) =>
        {
            string name = names.TryGetValue(x.UserId, out var n) ? n : x.UserId.ToString();
            string avg = x.AvgLen.ToString("0.0");
            return $"[{((page - 1) * 10) + index + 1}] | {name}: Avg length {avg} chars";
        });

        StringBuilder sb = new();
        sb.AppendLine("**Global Average Message Length Leaderboard** (all time)");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", lines));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");
        // User global rank by average message length (all time)
        string rankLine = "Your rank: N/A";
        var me = Context.DbUser;
        if (me != null)
        {
            var meAgg = dbContext.UserLevels.AsNoTracking()
                .Where(ul => ul.UserId == me.Id)
                .GroupBy(ul => ul.UserId)
                .Select(g => new { SumLen = g.Sum(ul => ul.UserAverageMessageLength * ul.UserMessageCount), SumCount = g.Sum(ul => ul.UserMessageCount) })
                .FirstOrDefault();
            if (meAgg != null && meAgg.SumCount > 0)
            {
                double myAvg = meAgg.SumLen / meAgg.SumCount;
                int better = dbContext.UserLevels.AsNoTracking()
                    .GroupBy(ul => ul.UserId)
                    .Select(g => new { SumLen = g.Sum(ul => ul.UserAverageMessageLength * ul.UserMessageCount), SumCount = g.Sum(ul => ul.UserMessageCount) })
                    .Where(x => x.SumCount > 0)
                    .Count(x => (x.SumLen / x.SumCount) > myAvg);
                rankLine = $"Your rank: #{better + 1}";
            }
        }
        sb.AppendLine(rankLine);
        await ReplyAsync(sb.ToString());
    }

    // ---------------------- Helper methods to reduce duplication ----------------------

    private bool ValidateDays(int days)
    {
        int maxDays = Env.Get<int>("ACTIVITY_GRAPHS_MAX_DAYS", 90);
        if (days <= 0 || days > maxDays)
        {
            ReplyAsync($"Please provide a number of days between 1 and {maxDays}.").GetAwaiter().GetResult();
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

            int maxDays = Env.Get<int>("ACTIVITY_GRAPHS_MAX_DAYS", 90);

            if (input.StartsWith("past") && input.EndsWith("days"))
            {
                var num = input.Substring(4, input.Length - 8);
                if (int.TryParse(num, out int parsed))
                {
                    if (parsed < 7) parsed = 7;
                    if (parsed > maxDays) parsed = maxDays;
                    return (true, parsed, (DateTime?)null);
                }
                await ReplyAsync($"Please provide a number of days between 7 and {maxDays} or a valid preset (past7days, past30days, past60days, past{maxDays}days).\nOr provide a date range like 2025-01-01..2025-01-31.");
                return (false, 0, null);
            }

            if (int.TryParse(input, out int asInt))
            {
                if (asInt < 7) asInt = 7;
                if (asInt > maxDays) asInt = maxDays;
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
                    if (span > maxDays)
                    {
                        await ReplyAsync($"Date range exceeds maximum of {maxDays} days.");
                        return (false, 0, null);
                    }
                    return (true, (int)span, DateTime.SpecifyKind(start, DateTimeKind.Utc));
                }
                await ReplyAsync($"Invalid date range format. Use YYYY-MM-DD..YYYY-MM-DD and ensure the range is at most {maxDays} days and start <= end.");
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
        var query = dbContext.UserActivity.AsNoTracking().Where(ua => ua.InsertDate >= start);
        if (!global && guildId.HasValue)
            query = query.Where(ua => ua.GuildId == guildId.Value);

        var top = query
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(x => x.XpGained) })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToList();

        var userIds = top.Select(t => t.UserId).ToList();
        if (userIds.Count == 0) return new List<dynamic>();

        var byDay = query
            .Where(ua => userIds.Contains(ua.UserId))
            .GroupBy(ua => new { ua.UserId, Day = ua.InsertDate.Date })
            .Select(g => new { g.Key.UserId, g.Key.Day, Xp = g.Sum(x => x.XpGained) })
            .ToList();

        var dict = byDay
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Day, x => (int)x.Xp));

        var perUser = top
            .Select(t => new { UserId = t.UserId, Total = (int)t.Total, ByDay = (IDictionary<DateTime, int>)(dict.TryGetValue(t.UserId, out var d) ? d : new Dictionary<DateTime, int>()) })
            .Cast<dynamic>()
            .ToList();

        return perUser;
    }

    private Dictionary<string, List<int>> BuildSeries(List<dynamic> perUser, DateTime start, int days, bool cumulative, bool global, int? guildId)
    {
        var series = new Dictionary<string, List<int>>();

        // If cumulative, compute baseline per user (activity before start)
        Dictionary<int, long> baselineMap = new();
        if (cumulative)
        {
            var userIds = perUser.Select(p => (int)p.UserId).ToList();
            var baseQuery = dbContext.UserActivity.AsNoTracking()
                .Where(ua => ua.InsertDate < start && userIds.Contains(ua.UserId));
            if (!global && guildId.HasValue)
                baseQuery = baseQuery.Where(ua => ua.GuildId == guildId.Value);
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
            var dbUser = dbContext.Users.AsNoTracking().FirstOrDefault(u => u.Id == userId);
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
    [RequireBotPermission(GuildPermission.AttachFiles)]
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

        var series = BuildSeries(perUser, start, daysVal, cumulative: false, global: false, guildId: guild.Id);
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
    [RequireBotPermission(GuildPermission.AttachFiles)]
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

        var series = BuildSeries(perUser, start, daysVal, cumulative: false, global: true, guildId: null);
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
        var discordIds = mentionedUsers.Where(mu => mu != null).Select(mu => (long)mu.Id).ToList();
        if (discordIds.Count == 0) return new List<dynamic>();

        var users = dbContext.Users.AsNoTracking()
            .Where(u => discordIds.Contains((long)u.DiscordId))
            .Select(u => new { u.Id, u.DiscordId })
            .ToList();
        var userIds = users.Select(u => u.Id).ToList();
        if (userIds.Count == 0) return new List<dynamic>();

        var query = dbContext.UserActivity.AsNoTracking()
            .Where(ua => ua.InsertDate >= start && userIds.Contains(ua.UserId));
        if (!global && guildId.HasValue)
            query = query.Where(ua => ua.GuildId == guildId.Value);

        var totals = query
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(x => x.XpGained) })
            .OrderByDescending(x => x.Total)
            .ToList();

        var byDay = query
            .GroupBy(ua => new { ua.UserId, Day = ua.InsertDate.Date })
            .Select(g => new { g.Key.UserId, g.Key.Day, Xp = g.Sum(x => x.XpGained) })
            .ToList();

        var dict = byDay
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Day, x => (int)x.Xp));

        var list = totals
            .Select(t => new { UserId = t.UserId, Total = (int)t.Total, ByDay = (IDictionary<DateTime, int>)(dict.TryGetValue(t.UserId, out var d) ? d : new Dictionary<DateTime, int>()) })
            .Cast<dynamic>()
            .ToList();

        return list;
    }
}
