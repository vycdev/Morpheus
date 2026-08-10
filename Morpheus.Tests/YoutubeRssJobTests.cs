using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Jobs;

namespace Morpheus.Tests;

public class YoutubeRssJobTests
{
    [Fact]
    public async Task HasFeedHistoryAsync_DistinguishesExistingChannelFromUnknownChannel()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        Assert.False(await YoutubeRssJob.HasFeedHistoryAsync(db, "channel-1"));

        db.YoutubeSeenVideos.Add(new YoutubeSeenVideo
        {
            YoutubeChannelId = "channel-1",
            VideoId = "video-that-is-no-longer-in-the-feed"
        });
        await db.SaveChangesAsync();

        bool hasHistory = await YoutubeRssJob.HasFeedHistoryAsync(db, "channel-1");

        Assert.True(hasHistory);
        Assert.False(await YoutubeRssJob.HasFeedHistoryAsync(db, "channel-2"));
    }

    [Fact]
    public async Task DispatchAsync_WhenOneDeliveryFails_ReportsFailureAndAttemptsEverySubscriber()
    {
        List<YoutubeSubscription> subscriptions =
        [
            new() { ChannelDiscordId = 1 },
            new() { ChannelDiscordId = 2 }
        ];
        List<ulong> attemptedChannels = [];

        bool succeeded = await YoutubeRssJob.DispatchAsync(
            subscriptions,
            subscription =>
            {
                attemptedChannels.Add(subscription.ChannelDiscordId);
                return Task.FromResult(subscription.ChannelDiscordId != 1);
            });

        Assert.False(succeeded);
        Assert.Equal([1UL, 2UL], attemptedChannels);
    }

    [Fact]
    public async Task DispatchAsync_RetriesFailedDeliveryUntilEverySubscriberSucceeds()
    {
        List<YoutubeSubscription> subscriptions =
        [
            new() { ChannelDiscordId = 1 },
            new() { ChannelDiscordId = 2 }
        ];
        int attempts = 0;

        Task<bool> SendAsync(YoutubeSubscription _)
        {
            attempts++;
            return Task.FromResult(attempts > subscriptions.Count);
        }

        bool firstSucceeded = await YoutubeRssJob.DispatchAsync(subscriptions, SendAsync);
        bool secondSucceeded = await YoutubeRssJob.DispatchAsync(subscriptions, SendAsync);

        Assert.False(firstSucceeded);
        Assert.True(secondSucceeded);
        Assert.Equal(4, attempts);
    }

    [Fact]
    public async Task DispatchAsync_WhenCallerCancels_PropagatesCancellationBeforeDelivery()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        bool sent = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            YoutubeRssJob.DispatchAsync(
                [new YoutubeSubscription()],
                _ =>
                {
                    sent = true;
                    return Task.FromResult(true);
                },
                cts.Token));

        Assert.False(sent);
    }
}
