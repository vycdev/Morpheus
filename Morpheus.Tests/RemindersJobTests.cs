using System.Reflection;
using Discord.WebSocket;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Jobs;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Tests;

public class RemindersJobTests
{
    [Fact]
    public async Task Execute_WhenCanceledBeforeLoadingDueReminders_PropagatesCancellation()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        using DiscordSocketClient discordClient = new();
        RemindersJob job = new(new LogsService(new LogQueue()), db, discordClient);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        IJobExecutionContext context = CreateContext(cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.Execute(context));
    }

    [Fact]
    public void Job_DisallowsConcurrentDeliveryRuns()
    {
        Assert.True(typeof(RemindersJob).IsDefined(typeof(DisallowConcurrentExecutionAttribute), inherit: false));
    }

    [Fact]
    public async Task DeliverAsync_WhenDeliveryFails_RetainsReminderForRetry()
    {
        Reminder reminder = new() { Id = 7, ChannelId = 42, Text = "Remember this" };
        List<string> logs = [];
        DateTime now = new(2026, 7, 31, 8, 0, 0, DateTimeKind.Utc);

        bool shouldDelete = await RemindersJob.DeliverAsync(
            reminder,
            _ => Task.FromException(new InvalidOperationException("Discord unavailable")),
            logs.Add,
            now);

        Assert.False(shouldDelete);
        Assert.Equal(1, reminder.DeliveryFailureCount);
        Assert.Equal(now, reminder.FirstDeliveryFailureAt);
        Assert.Equal(now.AddMinutes(1), reminder.NextDeliveryAttemptAt);
        Assert.Contains(logs, message => message.Contains("Retry 1 scheduled"));
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
            _ => { },
            DateTime.UtcNow);

        Assert.True(shouldDelete);
        Assert.Equal(["Remember this"], sentMessages);
    }

    [Fact]
    public async Task DeliverAsync_WhenChannelUnavailable_RetainsReminderForRetry()
    {
        Reminder reminder = new() { Id = 7, ChannelId = 42, Text = "Remember this" };
        List<string> logs = [];
        DateTime now = new(2026, 7, 31, 8, 0, 0, DateTimeKind.Utc);

        bool shouldDelete = await RemindersJob.DeliverAsync(
            reminder,
            null,
            logs.Add,
            now);

        Assert.False(shouldDelete);
        Assert.Equal(1, reminder.DeliveryFailureCount);
        Assert.Equal(now, reminder.FirstDeliveryFailureAt);
        Assert.Equal(now.AddMinutes(1), reminder.NextDeliveryAttemptAt);
        Assert.Contains(logs, message => message.Contains("unavailable") && message.Contains("Retry 1 scheduled"));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(11, 1024)]
    [InlineData(12, 1440)]
    [InlineData(100, 1440)]
    public void CalculateRetryDelay_DoublesAndCapsAtOneDay(int failureCount, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), RemindersJob.CalculateRetryDelay(failureCount));
    }

    [Fact]
    public async Task DeliverAsync_AtOneWeekDeadline_PermanentlyFailsWithoutSending()
    {
        DateTime firstFailure = new(2026, 7, 24, 8, 0, 0, DateTimeKind.Utc);
        Reminder reminder = new()
        {
            Id = 7,
            ChannelId = 42,
            Text = "Remember this",
            DeliveryFailureCount = 14,
            FirstDeliveryFailureAt = firstFailure,
            NextDeliveryAttemptAt = firstFailure.Add(RemindersJob.MaximumRetryAge)
        };
        bool sendAttempted = false;
        List<string> logs = [];

        bool shouldDelete = await RemindersJob.DeliverAsync(
            reminder,
            _ =>
            {
                sendAttempted = true;
                return Task.CompletedTask;
            },
            logs.Add,
            firstFailure.Add(RemindersJob.MaximumRetryAge));

        Assert.True(shouldDelete);
        Assert.False(sendAttempted);
        Assert.Contains(logs, message => message.Contains("permanently failed"));
    }

    private static IJobExecutionContext CreateContext(CancellationToken cancellationToken)
    {
        JobExecutionContextProxy.CurrentCancellationToken = cancellationToken;
        return DispatchProxy.Create<IJobExecutionContext, JobExecutionContextProxy>();
    }

    private class JobExecutionContextProxy : DispatchProxy
    {
        public static CancellationToken CurrentCancellationToken { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.ReturnType == typeof(CancellationToken))
                return CurrentCancellationToken;

            Type returnType = targetMethod?.ReturnType ?? typeof(void);
            return returnType == typeof(void) || !returnType.IsValueType
                ? null
                : Activator.CreateInstance(returnType);
        }
    }
}
