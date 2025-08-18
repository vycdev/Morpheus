using System.Text;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;

namespace Morpheus.Modules;

public class AdministratorModule(DiscordSocketClient client, DB dbContext) : ModuleBase<SocketCommandContextExtended>
{
    [Name("Dump Logs")]
    [Summary("Dumps the latest 25 logs from the database (bot owner only).")]
    [Command("dumplogs")]
    [RateLimit(3, 30)]
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
}
