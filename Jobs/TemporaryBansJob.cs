using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

public class TemporaryBansJob(LogsService logsService, DB db, DiscordSocketClient discordClient) : IJob
{
    private void Log(string message, LogSeverity severity = LogSeverity.Info) => logsService.Log($"Quartz Job - {message}", severity);

    public async Task Execute(IJobExecutionContext context)
    {
        DateTime now = DateTime.UtcNow;

        // Get pending temp bans that have expired and not yet unbanned
        var dueBans = await db.TemporaryBans
            .Where(tb => tb.UnbannedAt == null && tb.ExpiresAt <= now)
            .OrderBy(tb => tb.ExpiresAt)
            .ToListAsync();

        if (!dueBans.Any())
            return;

        foreach (var ban in dueBans)
        {
            try
            {
                var guild = discordClient.GetGuild(ban.GuildId);
                if (guild == null)
                {
                    Log($"Guild {ban.GuildId} not found for temp ban {ban.Id}. Marking as unbanned.", LogSeverity.Warning);
                    ban.UnbannedAt = now;
                    continue;
                }

                await guild.RemoveBanAsync(ban.UserId);
                ban.UnbannedAt = DateTime.UtcNow;
                Log($"Unbanned user {ban.UserId} from guild {ban.GuildId} (temp ban {ban.Id}).");
            }
            catch (Exception ex)
            {
                Log($"Failed to unban user {ban.UserId} from guild {ban.GuildId}: {ex.Message}", LogSeverity.Warning);
                // We do not set UnbannedAt here; it will retry next run
            }
        }

        await db.SaveChangesAsync();
    }
}


