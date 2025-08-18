using Discord.Commands;
using Microsoft.EntityFrameworkCore;
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
    [RateLimit(2, 60)]
    public async Task ActivityGraphAsync(int days = 7)
    {
        Guild? guild = Context.DbGuild;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        if (days <= 0 || days > 90)
        {
            await ReplyAsync("Please provide a number of days between 1 and 90.");
            return;
        }

        DateTime start = DateTime.UtcNow.Date.AddDays(-(days - 1));

        // Query activity in the period and group by user and day
        var q = dbContext.UserActivity
            .Where(ua => ua.GuildId == guild.Id && ua.InsertDate >= start)
            .AsEnumerable() // switch to in-memory to allow DateTime.Date grouping consistently
            .GroupBy(ua => new { ua.UserId, Day = ua.InsertDate.Date })
            .Select(g => new
            {
                UserId = g.Key.UserId,
                Day = g.Key.Day,
                Xp = g.Sum(x => x.XpGained)
            })
            .ToList();

        // Aggregate per user
        var perUser = q.GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Total = g.Sum(x => x.Xp),
                ByDay = g.ToDictionary(x => x.Day, x => x.Xp)
            })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToList();

        if (perUser.Count == 0)
        {
            await ReplyAsync("No activity data found for the requested period.");
            return;
        }

        // For each user, build a list of daily values (oldest -> newest)
        var series = new Dictionary<string, List<int>>();

        foreach (var userAgg in perUser)
        {
            var dbUser = dbContext.Users.Find(userAgg.UserId);
            string label = dbUser != null ? dbUser.Username : userAgg.UserId.ToString();
            List<int> values = new List<int>(new int[days]);
            for (int i = 0; i < days; i++)
            {
                DateTime day = start.AddDays(i);
                if (userAgg.ByDay.TryGetValue(day, out int xp)) values[i] = xp;
                else values[i] = 0;
            }
            series[label] = values;
        }

        byte[] png;
        try
        {
            png = ActivityGraphGenerator.GenerateLineChart(series, days);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ACTIVITY GRAPH ERROR] {ex}");
            await ReplyAsync("Failed to generate activity graph. Try again later.");
            return;
        }

        using var ms = new MemoryStream(png);
        await Context.Channel.SendFileAsync(ms, "activity_graph.png", $"Top {series.Count} users activity for the last {days} days");
    }

    [Name("Global Activity Graph")]
    [Summary("Generates a global activity graph for the top 10 users across all guilds over the past n days.")]
    [Command("globalactivitygraph")]
    [Alias("globalactgraph", "gact")]
    [RateLimit(2, 60)]
    public async Task GlobalActivityGraphAsync(int days = 7)
    {
        if (days <= 0 || days > 90)
        {
            await ReplyAsync("Please provide a number of days between 1 and 90.");
            return;
        }

        DateTime start = DateTime.UtcNow.Date.AddDays(-(days - 1));

        // Query activity in the period and group by user and day (across all guilds)
        var q = dbContext.UserActivity
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
            .ToList();

        if (perUser.Count == 0)
        {
            await ReplyAsync("No activity data found for the requested period.");
            return;
        }

        var series = new Dictionary<string, List<int>>();

        for (int u = 0; u < perUser.Count; u++)
        {
            var userAgg = perUser[u];
            var dbUser = dbContext.Users.Find(userAgg.UserId);
            string label = dbUser != null ? dbUser.Username : userAgg.UserId.ToString();
            List<int> values = new List<int>(new int[days]);
            for (int i = 0; i < days; i++)
            {
                DateTime day = start.AddDays(i);
                if (userAgg.ByDay.TryGetValue(day, out int xp)) values[i] = xp;
                else values[i] = 0;
            }
            series[label] = values;
        }

        byte[] png;
        try
        {
            png = ActivityGraphGenerator.GenerateLineChart(series, days);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GLOBAL ACTIVITY GRAPH ERROR] {ex}");
            await ReplyAsync("Failed to generate global activity graph. Try again later.");
            return;
        }

        using var ms2 = new MemoryStream(png);
        await Context.Channel.SendFileAsync(ms2, "global_activity_graph.png", $"Top {series.Count} users global activity for the last {days} days");
    }

    [Name("Leaderboard")]
    [Summary("Displays the leaderboard of users in the guild based on their levels.")]
    [Command("leaderboard")]
    [Alias("lb", "top", "topusers")]
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
}
