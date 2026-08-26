using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

[DisallowConcurrentExecution]
public class RemindersJob(LogsService logsService, DB dB, DiscordSocketClient discordClient) : IJob
{
    internal static readonly TimeSpan MaximumRetryAge = TimeSpan.FromDays(7);
    internal static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromHours(24);

    private void Log(string message) => logsService.Log($"Quartz Job - {message}");

    public async Task Execute(IJobExecutionContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        DateTime now = DateTime.UtcNow;

        // Find reminders that are due (due date <= now)
        var dueReminders = await dB.Reminders
            .Where(r => (r.NextDeliveryAttemptAt ?? r.DueDate) <= now)
            .OrderBy(r => r.NextDeliveryAttemptAt ?? r.DueDate)
            .ToListAsync(cancellationToken);

        if (!dueReminders.Any())
        {
            return;
        }

        await ProcessDueRemindersAsync(
            dueReminders,
            async reminder =>
            {
                // Find the channel in connected guilds
                var channel = discordClient.GetChannel(reminder.ChannelId) as IMessageChannel;
                Func<string, Task>? sendAsync = channel == null
                    ? null
                    : async content => await channel.SendMessageAsync(content);

                return await DeliverAsync(reminder, sendAsync, Log, now);
            },
            reminder => dB.Reminders.Remove(reminder),
            () => dB.SaveChangesAsync(CancellationToken.None),
            cancellationToken);
    }

    internal static async Task ProcessDueRemindersAsync(
        IReadOnlyList<Reminder> dueReminders,
        Func<Reminder, Task<bool>> deliverAsync,
        Action<Reminder> remove,
        Func<Task> saveChangesAsync,
        CancellationToken cancellationToken)
    {
        bool hasProcessedReminders = false;

        foreach (Reminder reminder in dueReminders)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // Discord delivery is an external side effect. Persist the
                // results of completed deliveries before honoring cancellation
                // so the next run does not send those reminders again.
                if (hasProcessedReminders)
                    await saveChangesAsync();

                cancellationToken.ThrowIfCancellationRequested();
            }

            bool shouldDelete = await deliverAsync(reminder);
            if (shouldDelete)
                remove(reminder);
            hasProcessedReminders = true;
        }

        if (hasProcessedReminders)
            await saveChangesAsync();

        // Cancellation may have arrived while the final reminder was being
        // delivered. Its state is durable now, so propagation is safe.
        cancellationToken.ThrowIfCancellationRequested();
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
