using Morpheus.Database.Models;
using Morpheus.Jobs;

namespace Morpheus.Tests;

public class RemindersJobTests
{
    [Fact]
    public async Task DeliverAsync_WhenDeliveryFails_RetainsReminderForRetry()
    {
        Reminder reminder = new() { Id = 7, ChannelId = 42, Text = "Remember this" };
        List<string> logs = [];

        bool shouldDelete = await RemindersJob.DeliverAsync(
            reminder,
            _ => Task.FromException(new InvalidOperationException("Discord unavailable")),
            logs.Add);

        Assert.False(shouldDelete);
        Assert.Contains(logs, message => message.Contains("Keeping reminder for retry."));
    }

    [Fact]
    public async Task DeliverAsync_WhenDeliverySucceeds_DeletesReminder()
    {
        Reminder reminder = new() { Id = 7, ChannelId = 42, Text = "Remember this" };
        List<string> sentMessages = [];

        bool shouldDelete = await RemindersJob.DeliverAsync(
            reminder,
            content =>
            {
                sentMessages.Add(content);
                return Task.CompletedTask;
            },
            _ => { });

        Assert.True(shouldDelete);
        Assert.Equal(["Remember this"], sentMessages);
    }
}