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
    public void Job_DisallowsConcurrentExecution()
    {
        Assert.True(Attribute.IsDefined(
            typeof(TemporaryBansJob), typeof(DisallowConcurrentExecutionAttribute)));
    }

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
    public async Task CompleteUnbanAsync_WhenRemovalSucceeds_MarksBanCompleted()
    {
        TemporaryBan ban = new();
        bool removalCalled = false;

        bool wasAlreadyUnbanned = await TemporaryBansJob.CompleteUnbanAsync(
            ban,
            () =>
            {
                removalCalled = true;
                return Task.CompletedTask;
            });

        Assert.True(removalCalled);
        Assert.False(wasAlreadyUnbanned);
        Assert.NotNull(ban.UnbannedAt);
    }

    [Fact]
    public async Task CompleteUnbanAsync_WhenRemovalIsForbidden_LeavesBanPending()
    {
        TemporaryBan ban = new();
        HttpException forbidden = new(
            HttpStatusCode.Forbidden,
            null!,
            DiscordErrorCode.UnknownBan,
            "Forbidden",
            []);

        HttpException thrown = await Assert.ThrowsAsync<HttpException>(() =>
            TemporaryBansJob.CompleteUnbanAsync(
                ban,
                () => Task.FromException(forbidden)));

        Assert.Same(forbidden, thrown);
        Assert.Null(ban.UnbannedAt);
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

    [Fact]
    public async Task ProcessDueBansAsync_CancellationAfterUnban_PersistsBeforeStopping()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection).Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();
        TemporaryBan first = new() { GuildId = 1, UserId = 2, ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        TemporaryBan second = new() { GuildId = 1, UserId = 3, ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        db.TemporaryBans.AddRange(first, second);
        await db.SaveChangesAsync();

        using CancellationTokenSource cancellation = new();
        int attempts = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TemporaryBansJob.ProcessDueBansAsync(
                [first, second],
                async ban =>
                {
                    attempts++;
                    await TemporaryBansJob.CompleteUnbanAsync(ban, () =>
                    {
                        cancellation.Cancel();
                        return Task.CompletedTask;
                    });
                    return true;
                },
                () => db.SaveChangesAsync(CancellationToken.None),
                (_, _) => Assert.Fail("Successful removal must not be reported as a failure"),
                cancellation.Token));

        db.ChangeTracker.Clear();
        Assert.Equal(1, attempts);
        Assert.NotNull((await db.TemporaryBans.SingleAsync(b => b.Id == first.Id)).UnbannedAt);
        Assert.Null((await db.TemporaryBans.SingleAsync(b => b.Id == second.Id)).UnbannedAt);
    }

    [Fact]
    public async Task ProcessDueBansAsync_SkipsFailuresButPersistsSuccessfulUnbans()
    {
        TemporaryBan unavailable = new();
        TemporaryBan completed = new();
        TemporaryBan failed = new();
        List<TemporaryBan> saved = [];
        List<TemporaryBan> failures = [];

        await TemporaryBansJob.ProcessDueBansAsync(
            [unavailable, completed, failed],
            ban => ban == failed
                ? Task.FromException<bool>(new InvalidOperationException("Discord unavailable"))
                : Task.FromResult(ban == completed),
            () =>
            {
                saved.Add(completed);
                return Task.CompletedTask;
            },
            (ban, _) => failures.Add(ban),
            CancellationToken.None);

        Assert.Equal([completed], saved);
        Assert.Equal([failed], failures);
    }

    [Fact]
    public async Task ProcessDueBansAsync_PersistenceFailure_StopsBeforeNextUnban()
    {
        TemporaryBan first = new();
        TemporaryBan second = new();
        List<TemporaryBan> attempted = [];
        InvalidOperationException failure = new("Database unavailable");

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TemporaryBansJob.ProcessDueBansAsync(
                [first, second],
                ban =>
                {
                    attempted.Add(ban);
                    return Task.FromResult(true);
                },
                () => Task.FromException(failure),
                (_, _) => Assert.Fail("Persistence errors must not be treated as Discord failures"),
                CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Equal([first], attempted);
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
