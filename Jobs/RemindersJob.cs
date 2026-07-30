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
    private void Log(string message) => logsService.Log($"Quartz Job - {message}");

    public async Task Execute(IJobExecutionContext context)
    {
        DateTime now = DateTime.UtcNow;

        // Find reminders that are due (due date <= now)
        var dueReminders = await dB.Reminders
            .Where(r => r.DueDate <= now)
            .OrderBy(r => r.DueDate)
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

            bool shouldDelete = await DeliverAsync(reminder, sendAsync, Log);
            if (shouldDelete)
                dB.Reminders.Remove(reminder);
        }

        // Persist deletions
        await dB.SaveChangesAsync();
    }

    internal static async Task<bool> DeliverAsync(
        Reminder reminder,
        Func<string, Task>? sendAsync,
        Action<string> log)
    {
        if (sendAsync == null)
        {
            log($"Channel {reminder.ChannelId} not found, deleting reminder {reminder.Id}.");
            return true;
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
            log($"Error sending reminder {reminder.Id} to channel {reminder.ChannelId}: {ex.Message}. Keeping reminder for retry.");
            return false;
        }
    }
}
