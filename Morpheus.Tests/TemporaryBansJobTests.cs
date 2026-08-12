using System.Net;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Jobs;
using Morpheus.Services;

namespace Morpheus.Tests;

public class TemporaryBansJobTests
{
    [Fact]
    public async Task CompleteUnbanAsync_WhenBanIsAlreadyMissing_MarksBanCompleted()
    {
        TemporaryBan ban = new();
        HttpException notFound = new(
            HttpStatusCode.NotFound,
            null!,
            DiscordErrorCode.UnknownBan,
            "Unknown Ban",
            []);

        bool wasAlreadyUnbanned = await TemporaryBansJob.CompleteUnbanAsync(
            ban,
            () => Task.FromException(notFound));

        Assert.True(wasAlreadyUnbanned);
        Assert.NotNull(ban.UnbannedAt);
    }

    [Fact]
    public async Task CompleteUnbanAsync_WhenDifferentDiscordResourceIsMissing_LeavesBanPending()
    {
        TemporaryBan ban = new();
        HttpException unknownGuild = new(
            HttpStatusCode.NotFound,
            null!,
            DiscordErrorCode.UnknownGuild,
            "Unknown Guild",
            []);

        HttpException thrown = await Assert.ThrowsAsync<HttpException>(() =>
            TemporaryBansJob.CompleteUnbanAsync(
                ban,
                () => Task.FromException(unknownGuild)));

        Assert.Same(unknownGuild, thrown);
        Assert.Null(ban.UnbannedAt);
    }

    [Fact]
    public async Task Execute_WhenGuildIsUnavailable_LeavesBanPendingForRetry()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        TemporaryBan ban = new()
        {
            GuildId = 1,
            UserId = 2,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        db.TemporaryBans.Add(ban);
        await db.SaveChangesAsync();

        using DiscordSocketClient discordClient = new();
        TemporaryBansJob job = new(new LogsService(new LogQueue()), db, discordClient);

        await job.Execute(null!);
        db.ChangeTracker.Clear();

        TemporaryBan persistedBan = await db.TemporaryBans.SingleAsync();
        Assert.Null(persistedBan.UnbannedAt);
    }
}
