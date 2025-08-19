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
            try
            {
                // Find the channel in connected guilds
                var channel = discordClient.GetChannel(reminder.ChannelId) as IMessageChannel;

                if (channel == null)
                {
                    Log($"Channel {reminder.ChannelId} not found, deleting reminder {reminder.Id}.");
                    dB.Reminders.Remove(reminder);
                    continue;
                }

                string content = reminder.Text ?? string.Empty;

                if (string.IsNullOrWhiteSpace(content))
                {
                    content = "Reminder!";
                }

                await channel.SendMessageAsync(content);

                Log($"Sent reminder {reminder.Id} to channel {reminder.ChannelId}");

                // Remove the reminder after sending
                dB.Reminders.Remove(reminder);
            }
            catch (Exception ex)
            {
                Log($"Error sending reminder {reminder.Id} to channel {reminder.ChannelId}: {ex.Message}. Deleting reminder.");
                try { dB.Reminders.Remove(reminder); }
                catch (Exception ex2)
                {
                    logsService.Log($"Failed to remove reminder {reminder.Id}: {ex2}", LogSeverity.Warning);
                }
            }
        }

        // Persist deletions
        await dB.SaveChangesAsync();
    }
}
