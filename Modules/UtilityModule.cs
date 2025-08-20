using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Utilities;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Morpheus.Modules;

public class UtilityModule(DB dbContext) : ModuleBase<SocketCommandContextExtended>
{
    private static readonly HttpClient httpClient = new();
    // DB is provided by primary-constructor style parameter
    // (accessible as 'dbContext' directly)

    [Name("Pin")]
    [Summary("Pins a message.")]
    [Command("pin")]
    [RateLimit(5, 30)]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    [RequireDbGuild]
    public async Task PinAsync([Remainder] string? _ = null)
    {
        // Get guild from db
        Guild? guild = Context.DbGuild!;

        // Check if the guild has a pins channel set
        if (guild.PinsChannelId == 0)
        {
            await ReplyAsync("Pins channel hasn't been set yet.");
            return;
        }

        // Get the pins channel
        SocketTextChannel? pinsChannel = Context.Guild.GetTextChannel(guild.PinsChannelId);

        if (pinsChannel == null)
        {
            await ReplyAsync("Pins channel couldn't be found.");
            return;
        }

        // Get the message the user replied to
        if (await Context.Channel.GetMessageAsync(Context.Message.ReferencedMessage.Id) is not IUserMessage message)
        {
            await ReplyAsync("Couldn't find the message you want to pin.");
            return;
        }

        // Make an embed of the message details
        EmbedBuilder embed = new()
        {
            Title = $"Pin in `#{message.Channel.Name}` by {Context.Message.Author.Username}",
            Url = message.GetJumpUrl(),
            Author = new EmbedAuthorBuilder()
            {
                Name = message.Author.Username,
                IconUrl = message.Author.GetAvatarUrl()
            },
            Description = message.Content,
            Color = Colors.Blue,
            Timestamp = message.CreatedAt
        };

        // Add image to embed
        if (message.Attachments.Count > 0)
            embed.ImageUrl = message.Attachments.First().Url;

        // Send the message to the pins channel
        await pinsChannel.SendMessageAsync(embed: embed.Build());

        // Send a confirmation message
        await ReplyAsync("Message pinned successfully.");

        return;
    }

    [Name("Reminder")]
    [Summary("Sets a reminder using a duration specification (e.g. '5 days and 3 hours'). Minimum 5 seconds, maximum 100 years. Usage: reminder <duration> [@user] [text...]. Example: reminder 5 days and 3 hours @User Take a break. Reminders are executed once a minute.")]
    [Command("reminder")]
    [Alias("settimer", "remindme")]
    [RateLimit(3, 10)]
    public async Task Reminder([Remainder] string input)
    {
        // input example: "5 days and 3 hours @User optional text here"
        if (string.IsNullOrWhiteSpace(input))
        {
            await ReplyAsync("Usage: reminder <duration> [@user] [text...]. Example: `reminder 5 days and 3 hours @User Take a break`.");
            return;
        }

        // No separate ping field — users can include mentions in the reminder text if desired.

        // Find all number+unit tokens
        var tokenPattern = new Regex("(\\d+)\\s*(years?|yrs?|y|months?|mos?|mo|weeks?|w|days?|d|hours?|hrs?|h|minutes?|mins?|m|seconds?|secs?|s)\\b", RegexOptions.IgnoreCase);
        var matches = tokenPattern.Matches(input);

        if (matches.Count == 0)
        {
            await ReplyAsync("Could not parse a duration. Examples: `5 days`, `3 hours and 30 minutes`, `7 weeks 2 minutes`. Supported units: seconds, minutes, hours, days, weeks, months, years.");
            return;
        }

        // Sum timespan
        double totalSeconds = 0;
        foreach (Match m in matches)
        {
            if (!long.TryParse(m.Groups[1].Value, out var number)) continue;
            string unit = m.Groups[2].Value.ToLowerInvariant();

            switch (unit)
            {
                case var u when u.StartsWith("y") || u.StartsWith("yr"):
                    totalSeconds += (double)number * 365 * 24 * 3600; // years -> 365 days
                    break;
                case var u when u.StartsWith("mo"):
                    totalSeconds += (double)number * 30 * 24 * 3600; // months -> 30 days
                    break;
                case var u when u.StartsWith("w"):
                    totalSeconds += (double)number * 7 * 24 * 3600;
                    break;
                case var u when u.StartsWith("d"):
                    totalSeconds += (double)number * 24 * 3600;
                    break;
                case var u when u.StartsWith("h"):
                    totalSeconds += (double)number * 3600;
                    break;
                case var u when u.StartsWith("m") && (u == "m" || u.StartsWith("min") || u.StartsWith("mins")):
                    totalSeconds += (double)number * 60;
                    break;
                case var u when u.StartsWith("s"):
                    totalSeconds += (double)number;
                    break;
                default:
                    break;
            }
        }

        if (totalSeconds <= 0)
        {
            await ReplyAsync("Parsed duration was zero. Please provide a valid duration.");
            return;
        }

        TimeSpan duration;
        try { duration = TimeSpan.FromSeconds(totalSeconds); }
        catch
        {
            await ReplyAsync("Duration too large or invalid.");
            return;
        }

        // Enforce min 1 minute and max 100 years
        var min = TimeSpan.FromMinutes(1);
        var max = TimeSpan.FromDays(365 * 100);

        if (duration < min)
        {
            await ReplyAsync("Minimum reminder is 1 minute.");
            return;
        }
        if (duration > max)
        {
            await ReplyAsync("Maximum reminder is 100 years.");
            return;
        }

        // Remove the duration tokens from input to get optional text
        input = tokenPattern.Replace(input, "").Trim();

        // After removing tokens and mention, remaining text is the reminder text
        string? text = string.IsNullOrWhiteSpace(input) ? null : input.Trim();

        // Require some text for the reminder
        if (string.IsNullOrWhiteSpace(text))
        {
            await ReplyAsync("You must provide some text for the reminder. If you want to mention someone, include the mention in the text.");
            return;
        }

        DateTime due = DateTime.UtcNow.Add(duration);

        // Create reminder record
        var reminder = new Reminder
        {
            ChannelId = Context.Channel.Id,
            Text = text,
            InsertDate = DateTime.UtcNow,
            DueDate = due,
            GuildId = Context.DbGuild?.Id,
            UserId = Context.DbUser?.Id
        };

        await dbContext.Reminders.AddAsync(reminder);
        await dbContext.SaveChangesAsync();

        await ReplyAsync($"Reminder scheduled for {due:yyyy-MM-dd HH:mm:ss} UTC.");
    }

}
