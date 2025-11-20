using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

[DisallowConcurrentExecution]
public class HoneypotRenameJob(LogsService logsService, DB dB, DiscordSocketClient discordClient) : IJob
{
    private static readonly Random _rng = new();

    private void Log(string message, LogSeverity severity = LogSeverity.Info) =>
        logsService.Log($"Quartz Job - {message}", severity);

    private static string GenerateRandomSuffix(int length = 8)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        StringBuilder sb = new(length);

        for (int i = 0; i < length; i++)
            sb.Append(chars[_rng.Next(chars.Length)]);

        return sb.ToString();
    }

    public static string GetHoneypotChannelName(string prefix = "honeypot", int suffixLen = 8)
    {
        string name = $"{prefix}-{GenerateRandomSuffix(suffixLen)}";
        // Ensure we only use allowed characters and trim the length to a sane limit
        name = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9-]", "-");
        if (name.Length > 90)
            name = name[..90];
        return name;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (discordClient.CurrentUser == null)
        {
            Log("Discord client not ready; skipping honeypot rename run.", LogSeverity.Warning);
            return;
        }
        DateTime now = DateTime.UtcNow;

        // Get all guilds that have honeypot enabled
        List<Guild> guilds = await dB.Guilds
            .Where(g => g.HoneypotChannelId != 0 && g.SendHoneypotMessages)
            .ToListAsync();

        if (!guilds.Any())
        {
            Log("No guilds configured for honeypot renaming.");
            return;
        }

        Log($"Attempting to rename honeypot channels for {guilds.Count} guild(s).");

        foreach (var guild in guilds)
        {
            try
            {
                SocketGuild? discordGuild = discordClient.GetGuild(guild.DiscordId);
                if (discordGuild == null)
                {
                    Log($"Guild with Discord ID {guild.DiscordId} not found.", LogSeverity.Warning);
                    continue;
                }

                SocketTextChannel? channel = discordGuild.GetTextChannel(guild.HoneypotChannelId);
                if (channel == null)
                {
                    Log($"Honeypot channel {guild.HoneypotChannelId} not found in guild {guild.Name} ({guild.DiscordId}).", LogSeverity.Warning);
                    continue;
                }

                // Check bot has manage channel permission - otherwise attempt will fail
                SocketGuildUser? botUser = discordGuild.GetUser(discordClient.CurrentUser.Id);
                if (botUser == null || !botUser.GuildPermissions.ManageChannels)
                {
                    Log($"Bot lacks ManageChannels permission in guild {guild.Name} ({guild.DiscordId}). Skipping.", LogSeverity.Warning);
                    continue;
                }

                string newName = GetHoneypotChannelName();
                if (channel.Name == newName)
                {
                    Log($"Channel {channel.Name} in guild {guild.Name} already has desired name. Skipping.");
                    continue;
                }

                await channel.ModifyAsync(props => props.Name = newName);
                Log($"Renamed honeypot channel {guild.HoneypotChannelId} in guild {guild.Name} to '{newName}'.");
            }
            catch (Exception ex)
            {
                Log($"Failed to rename honeypot channel for guild {guild.Name} ({guild.DiscordId}): {ex.Message}", LogSeverity.Warning);
            }

            // Small delay to avoid hitting global rate limits if running at scale
            await Task.Delay(700);
        }
    }
}
