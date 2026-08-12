using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

public class RemindersJob(LogsService logsService, DB dB, DiscordSocketClient discordClient) : IJob
{
    internal static readonly TimeSpan MaximumRetryAge = TimeSpan.FromDays(7);
    internal static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromHours(24);

    private void Log(string message) => logsService.Log($"Quartz Job - {message}");

    public async Task Execute(IJobExecutionContext context)
    {
        DateTime now = DateTime.UtcNow;

        // Find reminders that are due (due date <= now)
        var dueReminders = await dB.Reminders
            .Where(r => (r.NextDeliveryAttemptAt ?? r.DueDate) <= now)
            .OrderBy(r => r.NextDeliveryAttemptAt ?? r.DueDate)
            .ToListAsync();

        if (!dueReminders.Any())
        {
            return;
        }

        foreach (var reminder in dueReminders)
        {
            // Find the channel in connected guilds
            var channel = discordClient.GetChannel(reminder.ChannelId) as IMessageChannel;
            Func<string, Task>? sendAsync = channel == null
                ? null
                : async content => await channel.SendMessageAsync(content);

            bool shouldDelete = await DeliverAsync(reminder, sendAsync, Log, now);
            if (shouldDelete)
                dB.Reminders.Remove(reminder);
        }

        // Persist deletions
        await dB.SaveChangesAsync();
    }

    internal static async Task<bool> DeliverAsync(
        Reminder reminder,
        Func<string, Task>? sendAsync,
        Action<string> log,
        DateTime now)
    {
        if (reminder.FirstDeliveryFailureAt.HasValue &&
            now >= reminder.FirstDeliveryFailureAt.Value.Add(MaximumRetryAge))
        {
            log(
                $"Reminder {reminder.Id} to channel {reminder.ChannelId} permanently failed " +
                $"after {reminder.DeliveryFailureCount} delivery attempts over one week. Deleting reminder.");
            return true;
        }

        if (sendAsync == null)
        {
            ScheduleRetry(
                reminder,
                now,
                log,
                $"Channel {reminder.ChannelId} is unavailable for reminder {reminder.Id}");
            return false;
        }

        string content = string.IsNullOrWhiteSpace(reminder.Text) ? "Reminder!" : reminder.Text;

        try
        {
            await sendAsync(content);
            log($"Sent reminder {reminder.Id} to channel {reminder.ChannelId}");
            return true;
        }
        catch (Exception ex)
        {
            ScheduleRetry(
                reminder,
                now,
                log,
                $"Error sending reminder {reminder.Id} to channel {reminder.ChannelId}: {ex.Message}");
            return false;
        }
    }

    private static void ScheduleRetry(Reminder reminder, DateTime now, Action<string> log, string failure)
    {
        reminder.FirstDeliveryFailureAt ??= now;
        reminder.DeliveryFailureCount++;

        DateTime retryDeadline = reminder.FirstDeliveryFailureAt.Value.Add(MaximumRetryAge);
        DateTime nextAttempt = now.Add(CalculateRetryDelay(reminder.DeliveryFailureCount));
        reminder.NextDeliveryAttemptAt = nextAttempt < retryDeadline ? nextAttempt : retryDeadline;

        log($"{failure}. Retry {reminder.DeliveryFailureCount} scheduled for {reminder.NextDeliveryAttemptAt:u}.");
    }

    internal static TimeSpan CalculateRetryDelay(int failureCount)
    {
        if (failureCount <= 1)
            return TimeSpan.FromMinutes(1);

        // Attempt 12 would exceed 24 hours (2^11 minutes), so cap it and every
        // subsequent delay at one day.
        if (failureCount >= 12)
            return MaximumRetryDelay;

        return TimeSpan.FromMinutes(1 << (failureCount - 1));
    }
}
