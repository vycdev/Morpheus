using System.Text;
using Discord.Commands;
using Discord;
using Discord.WebSocket;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Extensions;

namespace Morpheus.Modules;

public class AdministratorModule(DiscordSocketClient client, DB dbContext) : ModuleBase<SocketCommandContextExtended>
{
    [Name("Dump Logs")]
    [Summary("Dumps the latest 25 logs from the database (bot owner only).")]
    [Command("dumplogs")]
    [RateLimit(3, 30)]
    [Hidden]
    public async Task DumpLogsAsync()
    {
        // Check OWNER_ID environment variable
        string? ownerEnv = Environment.GetEnvironmentVariable("OWNER_ID");
        if (string.IsNullOrWhiteSpace(ownerEnv) || !ulong.TryParse(ownerEnv, out var ownerId))
        {
            await ReplyAsync("Owner not configured.");
            return;
        }

        if (Context.User.Id != ownerId)
        {
            await ReplyAsync("You are not authorized to use this command.");
            return;
        }

        var logs = dbContext.Logs
            .OrderByDescending(l => l.InsertDate)
            .Take(25)
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

        const int maxMessageSize = 1900; // leave room for code fences
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            if (sb.Length + line.Length + 4 > maxMessageSize) // send current chunk
            {
                await ReplyAsync("```\n" + sb.ToString().TrimEnd() + "\n```");
                sb.Clear();
            }

            sb.AppendLine(line);
        }

        if (sb.Length > 0)
        {
            await ReplyAsync("```\n" + sb.ToString().TrimEnd() + "\n```");
        }
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
        string? ownerEnv = Environment.GetEnvironmentVariable("OWNER_ID");
        if (string.IsNullOrWhiteSpace(ownerEnv) || !ulong.TryParse(ownerEnv, out var ownerId))
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
        string? ownerEnv = Environment.GetEnvironmentVariable("OWNER_ID");
        if (string.IsNullOrWhiteSpace(ownerEnv) || !ulong.TryParse(ownerEnv, out var ownerId))
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
}
