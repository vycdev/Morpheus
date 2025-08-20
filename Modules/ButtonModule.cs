using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Utilities.Extensions;
using System.Text;

namespace Morpheus.Modules;

public class ButtonModule(DB dbContext) : ModuleBase<SocketCommandContextExtended>
{
    [Name("Press the Button")]
    [Summary("Press the button to gain points!")]
    [Command("pressbutton")]
    [Alias("button", "press")]
    [RateLimit(1, 30)]
    public async Task PressButton()
    {
        // Get guild from contenxt 
        Guild? guild = Context.DbGuild;
        // Get user from context
        User user = Context.DbUser!;

        // Get most recent button press time
        ButtonGamePress? buttonGamePress = await dbContext.ButtonGamePresses.OrderByDescending(b => b.InsertDate).FirstOrDefaultAsync();

        // Get the button press with the most points 
        ButtonGamePress? bestButtonPress = await dbContext.ButtonGamePresses.OrderByDescending(b => b.Score).FirstOrDefaultAsync();

        DateTime now = DateTime.UtcNow;
        long score = buttonGamePress == null ? 1 : (long)(now - buttonGamePress.InsertDate).TotalSeconds;

        ButtonGamePress buttonPress = new()
        {
            GuildId = guild?.Id,
            UserId = user.Id,
            InsertDate = DateTime.UtcNow,
            Score = score
        };

        dbContext.ButtonGamePresses.Add(buttonPress);

        if (buttonGamePress == null)
            await ReplyAsync($"Congratz you are the first one to press the button! You received the impossible score of {buttonPress.Score}.");
        else
        {
            if (buttonPress.Score > buttonGamePress.Score)
                await ReplyAsync($"You pressed the button! You received {buttonPress.Score} points. You beat the previous best score of {buttonGamePress.Score} points.");
            else
                await ReplyAsync($"You pressed the button! You received {buttonPress.Score} points. The most recent score was {buttonGamePress.Score} points.");
        }

        // Save changes to database
        await dbContext.SaveChangesAsync();
    }

    // Top global 
    [Name("Top Button Global")]
    [Summary("Get the top global button press scores.")]
    [Command("buttontopglobal")]
    [Alias("btopg", "topglobalbutton")]
    [RateLimit(1, 30)]
    public async Task TopGlobal()
    {
        // Get top 10 button presses 
        List<ButtonGamePress> buttonGamePresses = await dbContext.ButtonGamePresses
            .OrderByDescending(b => b.Score)
            .Take(10)
            .ToListAsync();

        if (buttonGamePresses.Count == 0)
        {
            await ReplyAsync("No button presses found.");
            return;
        }

        var entries = new List<string>();
        int idx = 1;
        foreach (ButtonGamePress buttonGamePress in buttonGamePresses)
        {
            User? user = await dbContext.Users.FindAsync(buttonGamePress.UserId);
            Guild? guild = await dbContext.Guilds.FindAsync(buttonGamePress.GuildId);
            string uname = user?.Username ?? buttonGamePress.UserId.ToString();
            string gname = guild?.Name ?? "(DM)";
            entries.Add($"[{idx}] | {uname} - {gname}: {buttonGamePress.Score}");
            idx++;
        }

        StringBuilder sb = new();
        sb.AppendLine("**Top 10 Global Button Presses:**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", entries));
        sb.AppendLine("```");
        await ReplyAsync(sb.ToString());
    }

    // Top guild 
    [Name("Top Button Guild")]
    [Summary("Get the top guild button press scores.")]
    [Command("buttontopguild")]
    [Alias("btopguild", "topguildbutton")]
    [RateLimit(1, 30)]
    public async Task TopGuild()
    {
        // Get guild from context 
        Guild guild = Context.DbGuild!;

        // Get top 10 button presses 
        List<ButtonGamePress> buttonGamePresses = await dbContext.ButtonGamePresses
            .Where(b => b.GuildId == guild.Id)
            .OrderByDescending(b => b.Score)
            .Take(10)
            .ToListAsync();

        if (buttonGamePresses.Count == 0)
        {
            await ReplyAsync("No button presses found.");
            return;
        }

        var entries = new List<string>();
        int idx = 1;
        foreach (ButtonGamePress buttonGamePress in buttonGamePresses)
        {
            User? user = await dbContext.Users.FindAsync(buttonGamePress.UserId);
            string uname = user?.Username ?? buttonGamePress.UserId.ToString();
            entries.Add($"[{idx}] | {uname}: {buttonGamePress.Score}");
            idx++;
        }

        StringBuilder sb = new();
        sb.AppendLine($"**Top 10 Button Presses in {guild.Name}:**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", entries));
        sb.AppendLine("```");
        await ReplyAsync(sb.ToString());
    }

    // Top individual user
    [Name("Top Button Individual User")]
    [Summary("Get the top individual button press scores.")]
    [Command("buttontopindividualuser")]
    [Alias("btopiu", "topindividualuserbutton")]
    [RateLimit(1, 30)]
    public async Task TopIndividualUser()
    {
        // Get top 10 button presses 
        List<ButtonGamePress> buttonGamePresses = await dbContext.ButtonGamePresses
            .Where(b => b.GuildId == null)
            .OrderByDescending(b => b.Score)
            .Take(10)
            .ToListAsync();

        if (buttonGamePresses.Count == 0)
        {
            await ReplyAsync("No button presses found.");
            return;
        }

        var entries = new List<string>();
        int idx = 1;
        foreach (ButtonGamePress buttonGamePress in buttonGamePresses)
        {
            User? user = await dbContext.Users.FindAsync(buttonGamePress.UserId);
            string uname = user?.Username ?? buttonGamePress.UserId.ToString();
            entries.Add($"[{idx}] | {uname}: {buttonGamePress.Score}");
            idx++;
        }

        StringBuilder sb = new();
        sb.AppendLine("**Top 10 Button Presses by Individual Users:**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", entries));
        sb.AppendLine("```");
        await ReplyAsync(sb.ToString());
    }

    // Top user
    [Name("Top Button User")]
    [Summary("Get the top user button press scores.")]
    [Command("buttontopuser")]
    [Alias("btopu", "topuserbutton")]
    [RateLimit(1, 30)]
    public async Task TopUser()
    {
        // Get user from context 
        User user = Context.DbUser!;

        // Get top 10 button presses 
        List<ButtonGamePress> buttonGamePresses = await dbContext.ButtonGamePresses
            .Where(b => b.UserId == user.Id)
            .OrderByDescending(b => b.Score)
            .Take(10)
            .ToListAsync();

        if (buttonGamePresses.Count == 0)
        {
            await ReplyAsync("No button presses found.");
            return;
        }

        var entries = new List<string>();
        int idx = 1;
        foreach (ButtonGamePress buttonGamePress in buttonGamePresses)
        {
            Guild? guild = await dbContext.Guilds.FindAsync(buttonGamePress.GuildId);
            string gname = guild?.Name ?? "(DM)";
            entries.Add($"[{idx}] | {gname}: {buttonGamePress.Score}");
            idx++;
        }

        StringBuilder sb = new();
        sb.AppendLine($"**Top 10 Button Presses by {user.Username}:**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", entries));
        sb.AppendLine("```");
        await ReplyAsync(sb.ToString());
    }

    // Top user guild
    [Name("Top Button User Guild")]
    [Summary("Get the top user button press scores in a guild.")]
    [Command("buttontopuserguild")]
    [Alias("btopug", "topuserguildbutton")]
    [RateLimit(1, 30)]
    public async Task TopUserGuild()
    {
        // Get user from context 
        User user = Context.DbUser!;
        // Get guild from context 
        Guild guild = Context.DbGuild!;

        // Get top 10 button presses 
        List<ButtonGamePress> buttonGamePresses = await dbContext.ButtonGamePresses
            .Where(b => b.UserId == user.Id && b.GuildId == guild.Id)
            .OrderByDescending(b => b.Score)
            .Take(10)
            .ToListAsync();

        if (buttonGamePresses.Count == 0)
        {
            await ReplyAsync("No button presses found.");
            return;
        }

        var entries = new List<string>();
        int idx = 1;
        foreach (ButtonGamePress buttonGamePress in buttonGamePresses)
        {
            entries.Add($"[{idx}] | {buttonGamePress.Score}");
            idx++;
        }

        StringBuilder sb = new();
        sb.AppendLine($"**Top 10 Button Presses by {user.Username} in {guild.Name}:**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", entries));
        sb.AppendLine("```");
        await ReplyAsync(sb.ToString());
    }

    // Top guild global 
    [Name("Top Button Guild Global")]
    [Summary("Get the top guild button press scores globally grouped by guild.")]
    [Command("buttontopguildglobal")]
    [Alias("btopgg", "topguildglobalbutton")]
    [RateLimit(1, 30)]
    public async Task TopGuildGlobal()
    {
        // Get top 10 button presses 
        var buttonGamePresses = await dbContext.ButtonGamePresses
            .GroupBy(b => b.GuildId)
            .Select(g => new { GuildId = g.Key, Score = g.Sum(b => b.Score) })
            .OrderByDescending(g => g.Score)
            .Take(10)
            .ToListAsync();

        if (buttonGamePresses.Count == 0)
        {
            await ReplyAsync("No button presses found.");
            return;
        }
        var entries = new List<string>();
        int idx = 1;
        foreach (var buttonGamePress in buttonGamePresses)
        {
            Guild? guild = await dbContext.Guilds.FindAsync(buttonGamePress.GuildId);
            string gname = guild?.Name ?? "(DM)";
            entries.Add($"[{idx}] | {gname}: {buttonGamePress.Score}");
            idx++;
        }

        StringBuilder sb = new();
        sb.AppendLine("**Top 10 Button Presses by Guild:**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", entries));
        sb.AppendLine("```");
        await ReplyAsync(sb.ToString());
    }

    // Current time since last press (score) 
    [Name("Current Time Since Last Press")]
    [Summary("Get the current time since last button press.")]
    [Command("buttoncurrenttime")]
    [Alias("bcurrenttime", "currenttimebutton")]
    [RateLimit(1, 30)]
    public async Task CurrentTimeSinceLastPress()
    {
        // Get most recent button press time
        ButtonGamePress? buttonGamePress = await dbContext.ButtonGamePresses
            .OrderByDescending(b => b.InsertDate)
            .FirstOrDefaultAsync();

        if (buttonGamePress == null)
        {
            await ReplyAsync("No button presses found.");
            return;
        }

        DateTime now = DateTime.UtcNow;
        long score = (long)(now - buttonGamePress.InsertDate).TotalSeconds;

        await ReplyAsync($"Current time since last button press: {buttonGamePress.InsertDate.GetAccurateTimeSpan(now)}");
        await ReplyAsync($"Current unclaimed score: {score}");
    }
}
