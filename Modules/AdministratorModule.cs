using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Extensions;
using Morpheus.Utilities;
using System.Text;

namespace Morpheus.Modules;

public class AdministratorModule(DiscordSocketClient client, DB dbContext) : MorpheusModuleBase
{
    [Name("Dump Logs")]
    [Summary("Dumps logs from the database (25 logs per page). (bot owner only).")]
    [Command("dumplogs")]
    [RateLimit(3, 30)]
    [Hidden]
    public async Task DumpLogsAsync(int page = 1)
    {
        // Check OWNER_ID environment variable
        if (!TryParseOwnerId(Env.Variables.GetValueOrDefault("OWNER_ID"), out var ownerId))
        {
            await ReplyAsync("Owner not configured.");
            return;
        }

        if (Context.User.Id != ownerId)
        {
            await ReplyAsync("You are not authorized to use this command.");
            return;
        }

        if (page < 1)
        {
            await ReplyAsync("Page must be 1 or greater.");
            return;
        }

        const int pageSize = 25;
        var logs = dbContext.Logs
            .OrderByDescending(l => l.InsertDate)
            .Skip(CalculateLogSkipCount(page, pageSize))
            .Take(pageSize)
            .ToList();

        if (!logs.Any())
        {
            await ReplyAsync("No logs found.");
            return;
        }

        // Build lines and send in code-block chunks to avoid Discord length limits
        var lines = new List<string>();
        foreach (var log in logs)
        {
            string time = log.InsertDate.ToString("yyyy-MM-dd HH:mm:ss");
            lines.Add($"[{time}] (v{log.Version}) [Severity:{log.Severity}] {log.Message}");
        }

        foreach (string message in BuildLogMessages(lines))
            await ReplyAsync(message);
    }

    [Name("Guild Count")]
    [Summary("Shows how many guilds the bot is currently in (bot owner only).")]
    [Command("guildcount")]
    [Alias("guilds", "servers")]
    [RateLimit(3, 30)]
    [Hidden]
    public async Task GuildCountAsync()
    {
        // Check OWNER_ID environment variable
        if (!TryParseOwnerId(Env.Variables.GetValueOrDefault("OWNER_ID"), out var ownerId))
        {
            await ReplyAsync("Owner not configured.");
            return;
        }

        if (Context.User.Id != ownerId)
        {
            await ReplyAsync("You are not authorized to use this command.");
            return;
        }

        int guildCount = client.Guilds.Count;
        await ReplyAsync($"I am currently in {guildCount} guild(s).");
    }

    [Name("Owner Send")]
    [Summary("Sends the provided text as the bot into the specified text channel (bot owner only).")]
    [Command("sendto")]
    [Alias("sendchan", "sayto")]
    [RateLimit(2, 10)]
    [Hidden]
    public async Task SendToChannelAsync(ulong channelId, [Remainder] string text)
    {
        // Check OWNER_ID environment variable
        if (!TryParseOwnerId(Env.Variables.GetValueOrDefault("OWNER_ID"), out var ownerId))
        {
            await ReplyAsync("Owner not configured.");
            return;
        }

        if (Context.User.Id != ownerId)
        {
            await ReplyAsync("You are not authorized to use this command.");
            return;
        }

        // Try to resolve the channel from cache first
        IMessageChannel? target = null;

        var maybe = Context.Client.GetChannel(channelId);
        if (maybe is IMessageChannel imc)
            target = imc;

        // If not found in cache, search guilds the client is in
        if (target == null)
        {
            foreach (var g in client.Guilds)
            {
                var ch = g.GetChannel(channelId) as IMessageChannel;
                if (ch != null)
                {
                    target = ch;
                    break;
                }
            }
        }

        if (target == null)
        {
            await ReplyAsync("Channel not found or the bot doesn't have access to it.");
            return;
        }

        try
        {
            await target.SendMessageAsync(text);
            await ReplyAsync($"Message sent to <#{channelId}>.");
        }
        catch (Exception ex)
        {
            await ReplyAsync($"Failed to send message: {ex.Message}");
        }
    }

    internal static int CalculateLogSkipCount(int page, int pageSize)
    {
        if (page <= 1)
            return 0;

        long skip = ((long)page - 1) * pageSize;
        return (int)Math.Min(skip, int.MaxValue);
    }

    internal static bool TryParseOwnerId(string? value, out ulong ownerId)
    {
        ownerId = 0;
        return !string.IsNullOrWhiteSpace(value) && ulong.TryParse(value, out ownerId);
    }

    internal static IReadOnlyList<string> BuildLogMessages(IEnumerable<string> lines)
    {
        const string prefix = "```\n";
        const string suffix = "\n```";
        const int maxMessageLength = 2000;
        int maxContentLength = maxMessageLength - prefix.Length - suffix.Length;

        var messages = new List<string>();
        var chunk = new StringBuilder();

        void Flush()
        {
            if (chunk.Length == 0)
                return;

            messages.Add(prefix + chunk + suffix);
            chunk.Clear();
        }

        foreach (string line in lines)
        {
            int offset = 0;

            while (offset < line.Length)
            {
                int separatorLength = chunk.Length > 0 ? 1 : 0;
                int available = maxContentLength - chunk.Length - separatorLength;

                if (available <= 0)
                {
                    Flush();
                    continue;
                }

                int take = Math.Min(available, line.Length - offset);
                if (offset + take < line.Length
                    && char.IsHighSurrogate(line[offset + take - 1])
                    && char.IsLowSurrogate(line[offset + take]))
                {
                    take--;
                }

                if (take == 0)
                {
                    Flush();
                    continue;
                }

                if (separatorLength > 0)
                    chunk.Append('\n');

                chunk.Append(line.AsSpan(offset, take));
                offset += take;

                if (offset < line.Length)
                    Flush();
            }
        }

        Flush();
        return messages;
    }
}
