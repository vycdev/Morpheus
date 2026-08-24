using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Jobs;
using Morpheus.Services;
using Quartz;
using System.Reflection;

namespace Morpheus.Tests;

public class TwitchLiveJobTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCanceledBeforeLoadingSubscriptions_PropagatesCancellation()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        LogsService logsService = new(new LogQueue());
        using HttpClient httpClient = new();
        TwitchService twitch = new(logsService, httpClient, "test-client", "test-secret");
        TwitchLiveJob job = new(db, twitch, new DiscordWebhookService(logsService), logsService);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        IJobExecutionContext context = CreateContext(cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.Execute(context));
    }

    [Fact]
    public async Task UpdateSubscriptionAsync_WhenAnnouncementFails_DoesNotRecordStreamId()
    {
        TwitchSubscription subscription = new()
        {
            IsLive = true,
            LastAnnouncedStreamId = "previous-stream"
        };
        TwitchService.TwitchStream stream = new("current-stream", "Test stream");

        bool changed = await TwitchLiveJob.UpdateSubscriptionAsync(
            subscription,
            stream,
            (_, _) => Task.FromResult(false));

        Assert.False(changed);
        Assert.Equal("previous-stream", subscription.LastAnnouncedStreamId);
    }

    [Fact]
    public async Task UpdateSubscriptionAsync_RetriesFailedAnnouncementUntilItSucceeds()
    {
        TwitchSubscription subscription = new();
        TwitchService.TwitchStream stream = new("current-stream", "Test stream");
        int attempts = 0;

        Task<bool> AnnounceAsync(TwitchSubscription _, TwitchService.TwitchStream __)
        {
            attempts++;
            return Task.FromResult(attempts > 1);
        }

        bool firstChanged = await TwitchLiveJob.UpdateSubscriptionAsync(subscription, stream, AnnounceAsync);
        bool secondChanged = await TwitchLiveJob.UpdateSubscriptionAsync(subscription, stream, AnnounceAsync);
        bool thirdChanged = await TwitchLiveJob.UpdateSubscriptionAsync(subscription, stream, AnnounceAsync);

        Assert.True(firstChanged);
        Assert.True(subscription.IsLive);
        Assert.True(secondChanged);
        Assert.False(thirdChanged);
        Assert.Equal(2, attempts);
        Assert.Equal("current-stream", subscription.LastAnnouncedStreamId);
    }

    [Fact]
    public async Task UpdateSubscriptionAsync_WhenCanceled_DoesNotAnnounceOrMutate()
    {
        TwitchSubscription subscription = new();
        TwitchService.TwitchStream stream = new("current-stream", "Test stream");
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        bool announced = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TwitchLiveJob.UpdateSubscriptionAsync(
                subscription,
                stream,
                (_, _) =>
                {
                    announced = true;
                    return Task.FromResult(true);
                },
                cancellation.Token));

        Assert.False(announced);
        Assert.False(subscription.IsLive);
        Assert.Null(subscription.LastAnnouncedStreamId);
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
