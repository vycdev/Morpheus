using Discord;
using Discord.Commands;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.Services;
using Morpheus.Utilities.Images;
using Morpheus.Utilities;

namespace Morpheus.Modules;

public class LevelsModule(
    DB dbContext,
    ActivityLeaderboardService activityLeaderboardService,
    ActivityGraphService activityGraphService) : ModuleBase<SocketCommandContextExtended>
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
        await SendUserActivityGraphAsync(days, mentionedUsers, global: false, cumulative: false, rollingWindowDays: null, "activity_graph.png");
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
        await SendUserActivityGraphAsync(days, mentionedUsers, global: false, cumulative: true, rollingWindowDays: null, "activity_graph_cumulative.png");
    }

    [Name("Global Activity Graph")]
    [Summary("Generates a global activity graph for the top 10 users across all guilds over the past n days.")]
    [Command("globalactivitygraph")]
    [Alias("globalactgraph", "gact")]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task GlobalActivityGraphAsync(string days = "past7days", params IUser[] mentionedUsers)
    {
        await SendUserActivityGraphAsync(days, mentionedUsers, global: true, cumulative: false, rollingWindowDays: null, "global_activity_graph.png");
    }

    [Name("Global Activity Graph (Cumulative)")]
    [Summary("Generates a global cumulative activity graph (running total) for the top 10 users across all guilds over the past n days.")]
    [Command("globalactivitygraphcumulative")]
    [Alias("globalactgraphcum", "gactcum")]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task GlobalActivityGraphCumulativeAsync(string days = "past7days", params IUser[] mentionedUsers)
    {
        await SendUserActivityGraphAsync(days, mentionedUsers, global: true, cumulative: true, rollingWindowDays: null, "global_activity_graph_cumulative.png");
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

        await ReplyLeaderboardResult(await activityLeaderboardService.GetGuildXpLeaderboardAsync(
            guild.Id,
            guild.Name,
            Context.DbUser?.Id,
            page));
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

        var dayResult = await TryNormalizeLeaderboardDaysAsync(days);
        if (!dayResult.success)
            return;

        await ReplyLeaderboardResult(await activityLeaderboardService.GetGuildPastXpLeaderboardAsync(
            guild.Id,
            guild.Name,
            Context.DbUser?.Id,
            dayResult.days,
            page));
    }

    [Name("Global Leaderboard")]
    [Summary("Displays the global leaderboard of users based on their levels across all guilds.")]
    [Command("globalleaderboard")]
    [Alias("globallb", "globaltop", "globaltopusers")]
    [RateLimit(3, 10)]
    public async Task GlobalLeaderboardAsync(int page = 1)
    {
        await ReplyLeaderboardResult(await activityLeaderboardService.GetGlobalXpLeaderboardAsync(Context.DbUser?.Id, page));
    }

    [Name("Global Leaderboard Past n Days")]
    [Summary("Displays the global leaderboard of users based on their levels across all guilds for the past n days.")]
    [Command("globalleaderboardpast")]
    [Alias("globallbp", "globaltoppast", "globaltopuserspast")]
    [RateLimit(3, 60)]
    public async Task GlobalLeaderboardPastAsync(int days, int page = 1)
    {
        var dayResult = await TryNormalizeLeaderboardDaysAsync(days);
        if (!dayResult.success)
            return;

        await ReplyLeaderboardResult(await activityLeaderboardService.GetGlobalPastXpLeaderboardAsync(
            Context.DbUser?.Id,
            dayResult.days,
            page));
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
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        await ReplyLeaderboardResult(await activityLeaderboardService.GetGuildMessageLeaderboardAsync(
            guild.Id,
            guild.Name,
            Context.DbUser?.Id,
            page));
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
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        var dayResult = await TryNormalizeLeaderboardDaysAsync(days);
        if (!dayResult.success)
            return;

        await ReplyLeaderboardResult(await activityLeaderboardService.GetGuildPastMessageLeaderboardAsync(
            guild.Id,
            guild.Name,
            Context.DbUser?.Id,
            dayResult.days,
            page));
    }

    [Name("Global Leaderboard Messages")]
    [Summary("Displays the global leaderboard by number of messages sent across all guilds (all time).")]
    [Command("globalleaderboardmessages")]
    [Alias("globallbm", "globaltopmessages", "globalmessageslb")]
    [RateLimit(3, 10)]
    public async Task GlobalLeaderboardMessagesAsync(int page = 1)
    {
        await ReplyLeaderboardResult(await activityLeaderboardService.GetGlobalMessageLeaderboardAsync(Context.DbUser?.Id, page));
    }

    [Name("Global Leaderboard Messages Past n Days")]
    [Summary("Displays the global leaderboard by number of messages sent across all guilds for the past n days.")]
    [Command("globalleaderboardmessagespast")]
    [Alias("globallbmp", "globaltopmessagespast", "globalmessageslbpast")]
    [RateLimit(3, 60)]
    public async Task GlobalLeaderboardMessagesPastAsync(int days, int page = 1)
    {
        var dayResult = await TryNormalizeLeaderboardDaysAsync(days);
        if (!dayResult.success)
            return;

        await ReplyLeaderboardResult(await activityLeaderboardService.GetGlobalPastMessageLeaderboardAsync(
            Context.DbUser?.Id,
            dayResult.days,
            page));
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
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        await ReplyLeaderboardResult(await activityLeaderboardService.GetGuildAverageLengthLeaderboardAsync(
            guild.Id,
            guild.Name,
            Context.DbUser?.Id,
            page));
    }

    [Name("Global Leaderboard Avg Message Length")]
    [Summary("Displays the global leaderboard by average message length across all guilds (all time). Weighted by message count.")]
    [Command("globalleaderboardavglength")]
    [Alias("globallbal", "globaltopavglength", "globalavglenlb")]
    [RateLimit(3, 10)]
    public async Task GlobalLeaderboardAvgLengthAsync(int page = 1)
    {
        await ReplyLeaderboardResult(await activityLeaderboardService.GetGlobalAverageLengthLeaderboardAsync(Context.DbUser?.Id, page));
    }

    [Name("Invalidate Message XP")]
    [Summary("Administrator-only: invalidates the XP of the message you reply to by setting its XP to 0 and adjusting totals.")]
    [Command("invalidatexp")]
    [Alias("invalidate", "zeroxp")]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RateLimit(3, 30)]
    public async Task InvalidateMessageXpAsync()
    {
        var guild = Context.DbGuild;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        // Ensure this command is used as a reply
        if (Context.Message.ReferencedMessage == null)
        {
            await ReplyAsync("Please reply to the message whose XP you want to invalidate.");
            return;
        }

        // Fetch the referenced message to confirm and get its ID
        if (await Context.Channel.GetMessageAsync(Context.Message.ReferencedMessage.Id) is not IUserMessage refMsg)
        {
            await ReplyAsync("Couldn't resolve the referenced message in this channel.");
            return;
        }

        ulong refMsgId = refMsg.Id;
        ulong channelId = Context.Channel.Id;

        // Find the single activity record for this message
        var activity = dbContext.UserActivity
            .FirstOrDefault(ua => ua.GuildId == guild.Id
                                && ua.DiscordChannelId == channelId
                                && ua.DiscordMessageId == refMsgId);

        if (activity == null)
        {
            await ReplyAsync("No activity record found for the referenced message.");
            return;
        }

        int removed = activity.XpGained;
        int userId = activity.UserId;
        if (removed <= 0)
        {
            await ReplyAsync("XP was already 0 for the referenced message.");
            return;
        }

        activity.XpGained = 0;
        await dbContext.SaveChangesAsync();

        // Adjust UserLevels totals and level for the affected user in this guild
        var ul = dbContext.UserLevels.FirstOrDefault(ul => ul.GuildId == guild.Id && ul.UserId == userId);
        if (ul != null)
        {
            long newTotal = Math.Max(0, (long)ul.TotalXp - removed);
            ul.TotalXp = (int)newTotal;
            ul.Level = ActivityHandler.CalculateLevel(ul.TotalXp);
            await dbContext.SaveChangesAsync();
        }

        await ReplyAsync($"Invalidated XP for the referenced message. Removed {removed} XP from totals.");
    }

    // ---------------------- Helper methods to reduce duplication ----------------------

    private bool IsOwner()
    {
        ulong ownerId = Env.Get<ulong>("OWNER_ID", 0);
        return ownerId != 0 && Context.User != null && Context.User.Id == ownerId;
    }

    private async Task ReplyLeaderboardResult(ActivityLeaderboardQueryResult result)
    {
        if (!result.Success)
        {
            await ReplyAsync(result.ErrorMessage ?? "Something went wrong.");
            return;
        }

        await ReplyAsync(ActivityLeaderboardService.FormatLeaderboardMessage(result.Page!));
    }

    private async Task<(bool success, int days)> TryNormalizeLeaderboardDaysAsync(int days)
    {
        int maxDays = Env.Get<int>("ACTIVITY_GRAPHS_MAX_DAYS", 90);
        if (days <= 0)
        {
            await ReplyAsync("Please provide a valid number of days greater than 0.");
            return (false, days);
        }

        if (!IsOwner() && days > maxDays)
        {
            await ReplyAsync($"Capping to maximum of {maxDays} days.");
            return (true, maxDays);
        }

        return (true, days);
    }

    private async Task SendUserActivityGraphAsync(
        string days,
        IUser[] mentionedUsers,
        bool global,
        bool cumulative,
        int? rollingWindowDays,
        string filename)
    {
        var rangeResult = await TryResolveActivityGraphRangeAsync(days);
        if (!rangeResult.success)
            return;

        int? guildId = null;
        if (!global)
        {
            Guild? guild = Context.DbGuild;
            if (guild == null)
            {
                await ReplyAsync("Guild not found.");
                return;
            }

            guildId = guild.Id;
        }

        ActivityGraphBuildResult graph = await activityGraphService.BuildUserActivityGraphAsync(
            rangeResult.range!,
            guildId,
            global,
            mentionedUsers?.Where(user => user != null).Select(user => user.Id) ?? Enumerable.Empty<ulong>(),
            cumulative,
            rollingWindowDays);

        await ReplyActivityGraphResult(graph, filename);
    }

    private async Task SendGuildActivityGraphAsync(
        string days,
        bool cumulative,
        int? rollingWindowDays,
        string filename)
    {
        var rangeResult = await TryResolveActivityGraphRangeAsync(days);
        if (!rangeResult.success)
            return;

        Guild? guild = Context.DbGuild;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        ActivityGraphBuildResult graph = await activityGraphService.BuildGuildActivityGraphAsync(
            rangeResult.range!,
            guild.Id,
            cumulative,
            rollingWindowDays);

        await ReplyActivityGraphResult(graph, filename);
    }

    private async Task<(bool success, ActivityGraphRange? range)> TryResolveActivityGraphRangeAsync(string days)
    {
        int maxDays = Env.Get<int>("ACTIVITY_GRAPHS_MAX_DAYS", 90);
        ActivityGraphParseResult parse = ActivityGraphService.ParseDaysString(days, IsOwner(), maxDays);

        if (!parse.Success)
        {
            await ReplyAsync(parse.ErrorMessage ?? "Unrecognized days parameter.");
            return (false, null);
        }

        return (true, ActivityGraphService.ResolveRange(parse));
    }

    private async Task ReplyActivityGraphResult(ActivityGraphBuildResult graph, string filename)
    {
        if (!graph.HasData)
        {
            await ReplyAsync("No activity data found for the requested period.");
            return;
        }

        await GenerateAndSendGraph(graph.Series, graph.Days, filename, graph.Message, graph.Start);
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
        await SendUserActivityGraphAsync(days, mentionedUsers, global: false, cumulative: false, rollingWindowDays: 7, "activity_graph_7day.png");
    }

    [Name("Global Activity Graph (7-day roll)")]
    [Summary("Generates a global 7-day rolling average activity graph for the top 10 users over the past n days.")]
    [Command("globalactivitygraph7day")]
    [Alias("globalactgraph7", "gact7")]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task GlobalActivityGraph7DayAsync(string days = "past7days", params IUser[] mentionedUsers)
    {
        await SendUserActivityGraphAsync(days, mentionedUsers, global: true, cumulative: false, rollingWindowDays: 7, "global_activity_graph_7day.png");
    }

    [Name("Activity Graph (30-day roll)")]
    [Summary("Generates a 30-day rolling average activity graph for the top 10 users over the past n days.")]
    [Command("activitygraph30day")]
    [Alias("actgraph30", "ag30")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task ActivityGraph30DayAsync(string days = "past30days", params IUser[] mentionedUsers)
    {
        await SendUserActivityGraphAsync(days, mentionedUsers, global: false, cumulative: false, rollingWindowDays: 30, "activity_graph_30day.png");
    }

    [Name("Global Activity Graph (30-day roll)")]
    [Summary("Generates a global 30-day rolling average activity graph for the top 10 users over the past n days.")]
    [Command("globalactivitygraph30day")]
    [Alias("globalactgraph30", "gact30")]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task GlobalActivityGraph30DayAsync(string days = "past30days", params IUser[] mentionedUsers)
    {
        await SendUserActivityGraphAsync(days, mentionedUsers, global: true, cumulative: false, rollingWindowDays: 30, "global_activity_graph_30day.png");
    }

    [Name("Activity Graph (90-day roll)")]
    [Summary("Generates a 90-day rolling average activity graph for the top 10 users over the past n days.")]
    [Command("activitygraph90day")]
    [Alias("actgraph90", "ag90")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task ActivityGraph90DayAsync(string days = "past90days", params IUser[] mentionedUsers)
    {
        await SendUserActivityGraphAsync(days, mentionedUsers, global: false, cumulative: false, rollingWindowDays: 90, "activity_graph_90day.png");
    }

    [Name("Global Activity Graph (90-day roll)")]
    [Summary("Generates a global 90-day rolling average activity graph for the top 10 users over the past n days.")]
    [Command("globalactivitygraph90day")]
    [Alias("globalactgraph90", "gact90")]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task GlobalActivityGraph90DayAsync(string days = "past90days", params IUser[] mentionedUsers)
    {
        await SendUserActivityGraphAsync(days, mentionedUsers, global: true, cumulative: false, rollingWindowDays: 90, "global_activity_graph_90day.png");
    }

    [Name("Activity Graph (180-day roll)")]
    [Summary("Generates a 180-day rolling average activity graph for the top 10 users over the past n days.")]
    [Command("activitygraph180day")]
    [Alias("actgraph180", "ag180")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task ActivityGraph180DayAsync(string days = "past180days", params IUser[] mentionedUsers)
    {
        await SendUserActivityGraphAsync(days, mentionedUsers, global: false, cumulative: false, rollingWindowDays: 180, "activity_graph_180day.png");
    }

    [Name("Global Activity Graph (180-day roll)")]
    [Summary("Generates a global 180-day rolling average activity graph for the top 10 users over the past n days.")]
    [Command("globalactivitygraph180day")]
    [Alias("globalactgraph180", "gact180")]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task GlobalActivityGraph180DayAsync(string days = "past180days", params IUser[] mentionedUsers)
    {
        await SendUserActivityGraphAsync(days, mentionedUsers, global: true, cumulative: false, rollingWindowDays: 180, "global_activity_graph_180day.png");
    }

    [Name("Guild Activity Graph")]
    [Summary("Generates an activity graph showing the overall guild activity over the past n days.")]
    [Command("guildactivitygraph")]
    [Alias("guildactgraph", "gag")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task GuildActivityGraphAsync(string days = "past7days")
    {
        await SendGuildActivityGraphAsync(days, cumulative: false, rollingWindowDays: null, "guild_activity_graph.png");
    }

    [Name("Guild Activity Graph (Cumulative)")]
    [Summary("Generates a cumulative activity graph (running total) showing the overall guild activity over the past n days.")]
    [Command("guildactivitygraphcumulative")]
    [Alias("guildactgraphcum", "gagcum")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task GuildActivityGraphCumulativeAsync(string days = "past7days")
    {
        await SendGuildActivityGraphAsync(days, cumulative: true, rollingWindowDays: null, "guild_activity_graph_cumulative.png");
    }

    [Name("Guild Activity Graph (7-day roll)")]
    [Summary("Generates a 7-day rolling average activity graph showing the overall guild activity over the past n days.")]
    [Command("guildactivitygraph7day")]
    [Alias("guildactgraph7", "gag7")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task GuildActivityGraph7DayAsync(string days = "past7days")
    {
        await SendGuildActivityGraphAsync(days, cumulative: false, rollingWindowDays: 7, "guild_activity_graph_7day.png");
    }

    [Name("Guild Activity Graph (30-day roll)")]
    [Summary("Generates a 30-day rolling average activity graph showing the overall guild activity over the past n days.")]
    [Command("guildactivitygraph30day")]
    [Alias("guildactgraph30", "gag30")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task GuildActivityGraph30DayAsync(string days = "past30days")
    {
        await SendGuildActivityGraphAsync(days, cumulative: false, rollingWindowDays: 30, "guild_activity_graph_30day.png");
    }

    [Name("Guild Activity Graph (90-day roll)")]
    [Summary("Generates a 90-day rolling average activity graph showing the overall guild activity over the past n days.")]
    [Command("guildactivitygraph90day")]
    [Alias("guildactgraph90", "gag90")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task GuildActivityGraph90DayAsync(string days = "past90days")
    {
        await SendGuildActivityGraphAsync(days, cumulative: false, rollingWindowDays: 90, "guild_activity_graph_90day.png");
    }

    [Name("Guild Activity Graph (180-day roll)")]
    [Summary("Generates a 180-day rolling average activity graph showing the overall guild activity over the past n days.")]
    [Command("guildactivitygraph180day")]
    [Alias("guildactgraph180", "gag180")]
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 60)]
    public async Task GuildActivityGraph180DayAsync(string days = "past180days")
    {
        await SendGuildActivityGraphAsync(days, cumulative: false, rollingWindowDays: 180, "guild_activity_graph_180day.png");
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

}
