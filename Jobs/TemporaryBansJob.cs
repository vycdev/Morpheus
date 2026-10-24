using System.Net;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

[DisallowConcurrentExecution]
public class TemporaryBansJob(LogsService logsService, DB db, DiscordSocketClient discordClient) : IJob
{
    private void Log(string message, LogSeverity severity = LogSeverity.Info) => logsService.Log($"Quartz Job - {message}", severity);

    public async Task Execute(IJobExecutionContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        DateTime now = DateTime.UtcNow;

        // Get pending temp bans that have expired and not yet unbanned
        var dueBans = await db.TemporaryBans
            .Where(tb => tb.UnbannedAt == null && tb.ExpiresAt <= now)
            .OrderBy(tb => tb.ExpiresAt)
            .ToListAsync(cancellationToken);

        if (!dueBans.Any())
            return;

        await ProcessDueBansAsync(
            dueBans,
            async ban =>
            {
                var guild = discordClient.GetGuild(ban.GuildId);
                if (guild == null)
                {
                    Log($"Guild {ban.GuildId} not found for temp ban {ban.Id}. Will retry later.", LogSeverity.Warning);
                    return false;
                }

                bool wasAlreadyUnbanned = await CompleteUnbanAsync(
                    ban,
                    () => guild.RemoveBanAsync(
                        ban.UserId,
                        new RequestOptions { CancelToken = cancellationToken }));
                Log(wasAlreadyUnbanned
                    ? $"User {ban.UserId} was already unbanned from guild {ban.GuildId} (temp ban {ban.Id})."
                    : $"Unbanned user {ban.UserId} from guild {ban.GuildId} (temp ban {ban.Id}).");
                return true;
            },
            // Persist each Discord side effect even if Quartz cancels before
            // the next ban, so a retry cannot repeat the removal.
            () => db.SaveChangesAsync(CancellationToken.None),
            (ban, ex) => Log($"Failed to unban user {ban.UserId} from guild {ban.GuildId}: {ex.Message}", LogSeverity.Warning),
            cancellationToken);
    }

    internal static async Task ProcessDueBansAsync(
        IReadOnlyList<TemporaryBan> dueBans,
        Func<TemporaryBan, Task<bool>> unbanAsync,
        Func<Task> saveChangesAsync,
        Action<TemporaryBan, Exception> logFailure,
        CancellationToken cancellationToken)
    {
        foreach (TemporaryBan ban in dueBans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool completed;
            try
            {
                completed = await unbanAsync(ban);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logFailure(ban, ex);
                continue;
            }

            // Persistence errors must stop the job rather than allowing more
            // unbans without a durable record of which ones completed.
            if (completed)
                await saveChangesAsync();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static async Task<bool> CompleteUnbanAsync(
        TemporaryBan ban,
        Func<Task> removeBanAsync)
    {
        bool wasAlreadyUnbanned = false;
        try
        {
            await removeBanAsync();
        }
        catch (HttpException ex) when (
            ex.HttpCode == HttpStatusCode.NotFound &&
            ex.DiscordCode == DiscordErrorCode.UnknownBan)
        {
            // A moderator may have removed the ban before this job ran. Discord's
            // Unknown Ban response means the desired state is already reached.
            wasAlreadyUnbanned = true;
        }

        ban.UnbannedAt = DateTime.UtcNow;
        return wasAlreadyUnbanned;
    }
}


