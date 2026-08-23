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
using Quartz;
using System.Reflection;

namespace Morpheus.Tests;

public class TemporaryBansJobTests
{
    [Fact]
    public async Task Execute_WhenCanceledBeforeLoadingDueBans_PropagatesCancellation()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        using DiscordSocketClient discordClient = new();
        TemporaryBansJob job = new(new LogsService(new LogQueue()), db, discordClient);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        IJobExecutionContext context = CreateContext(cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.Execute(context));
    }

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

        await job.Execute(CreateContext(CancellationToken.None));
        db.ChangeTracker.Clear();

        TemporaryBan persistedBan = await db.TemporaryBans.SingleAsync();
        Assert.Null(persistedBan.UnbannedAt);
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
