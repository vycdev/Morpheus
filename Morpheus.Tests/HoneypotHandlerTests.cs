using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Handlers;

namespace Morpheus.Tests;

public class HoneypotHandlerTests
{
    [Fact]
    public async Task ExecuteTemporaryBanWorkflow_WhenNotificationFails_BanRemainsRecorded()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        List<string> calls = [];
        InvalidOperationException notificationError = new("Channel unavailable");
        Exception? loggedError = null;
        DateTime bannedAt = new(2026, 8, 9, 12, 34, 56, DateTimeKind.Utc);

        await HoneypotHandler.ExecuteTemporaryBanWorkflowAsync(
            banUser: () =>
            {
                calls.Add("ban");
                return Task.CompletedTask;
            },
            persistTemporaryBan: async () =>
            {
                calls.Add("persist");
                await HoneypotHandler.RecordTemporaryBanAsync(db, guildId: 1, userId: 2, bannedAt);
            },
            sendNotification: () =>
            {
                calls.Add("notify");
                return Task.FromException(notificationError);
            },
            logNotificationFailure: ex => loggedError = ex
        );

        db.ChangeTracker.Clear();
        TemporaryBan persistedBan = await db.TemporaryBans.SingleAsync();

        Assert.Equal(["ban", "persist", "notify"], calls);
        Assert.Same(notificationError, loggedError);
        Assert.Equal((ulong)1, persistedBan.GuildId);
        Assert.Equal((ulong)2, persistedBan.UserId);
        Assert.Equal(bannedAt.AddDays(7), persistedBan.ExpiresAt);
        Assert.Null(persistedBan.UnbannedAt);
    }

    [Fact]
    public async Task ExecuteTemporaryBanWorkflow_WhenPersistenceFails_DoesNotSendNotification()
    {
        List<string> calls = [];
        InvalidOperationException persistenceError = new("Database unavailable");

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HoneypotHandler.ExecuteTemporaryBanWorkflowAsync(
                banUser: () =>
                {
                    calls.Add("ban");
                    return Task.CompletedTask;
                },
                persistTemporaryBan: () =>
                {
                    calls.Add("persist");
                    return Task.FromException(persistenceError);
                },
                sendNotification: () =>
                {
                    calls.Add("notify");
                    return Task.CompletedTask;
                },
                logNotificationFailure: _ => { }
            )
        );

        Assert.Same(persistenceError, thrown);
        Assert.Equal(["ban", "persist"], calls);
    }
}
