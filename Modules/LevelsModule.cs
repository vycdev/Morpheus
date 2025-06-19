using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        var user = Context.DbUser;
        var guild = Context.DbGuild;

        if (user == null || guild == null)
        {
            await ReplyAsync("User or guild not found.");
            return;
        }

        IQueryable<UserLevels> userLevels = dbContext.UserLevels
            .Where(ul => ul.UserId == user.Id);

        UserLevels? userLevelGuild = userLevels
            .FirstOrDefault(ul => ul.GuildId == guild.Id);

        if (userLevels.Count() == 0)
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

    [Name("Leaderboard")]
    [Summary("Displays the leaderboard of users in the guild based on their levels.")]
    [Command("leaderboard")]
    [Alias("lb", "top", "topusers")]
    [RateLimit(3, 10)]
    public async Task LeaderboardAsync(int page = 1)
    {
        var guild = Context.DbGuild;
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

        var leaderboard = userLevels
            .Skip((page - 1) * 10)
            .Take(10)
            .Include(u => u.User)
            .ToList()
            .Select((ul, index) => $"[{((page - 1) * 10) + index + 1}] | {ul.User.Username}: Level {ul.Level} with {ul.TotalXp} XP");

        StringBuilder sb = new();

        sb.AppendLine($"**Leaderboard for {guild.Name}**");
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
                Level = g.Max(ul => ul.Level),
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

        var leaderboard = userLevels
            .Skip((page - 1) * 10)
            .Take(10)
            .ToList()
            .Select((ul, index) => $"[{((page - 1) * 10) + index + 1}] | {ul.User.Username}: Level {ul.Level} with {ul.TotalXp} XP");

        StringBuilder sb = new();

        sb.AppendLine("**Global Leaderboard**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");

        await ReplyAsync(sb.ToString());
    }
}
