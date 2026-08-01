using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Jobs;
using Morpheus.Services;

namespace Morpheus.Tests;

public class RssFeedJobTests
{
    [Fact]
    public async Task HasFeedHistoryAsync_WhenOlderEntryRolledOutOfFeed_ReturnsTrue()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();
        db.RssSeenEntries.Add(new RssSeenEntry
        {
            FeedUrl = "https://example.com/feed",
            EntryId = "entry-that-is-no-longer-in-the-feed"
        });
        await db.SaveChangesAsync();

        bool hasHistory = await RssFeedJob.HasFeedHistoryAsync(db, "https://example.com/feed");

        Assert.True(hasHistory);
    }

    [Fact]
    public async Task DispatchAsync_WhenOneDeliveryFails_ReportsFailureAndAttemptsEverySubscriber()
    {
        RssFeedService.FeedEntry entry = new("entry-1", "Entry", "https://example.com/entry-1", DateTime.UtcNow);
        List<RssSubscription> subscriptions =
        [
            new() { ChannelDiscordId = 1 },
            new() { ChannelDiscordId = 2 }
        ];
        List<ulong> attemptedChannels = [];

        bool succeeded = await RssFeedJob.DispatchAsync(
            entry,
            subscriptions,
            (subscription, _) =>
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
        RssFeedService.FeedEntry entry = new("entry-1", "Entry", "https://example.com/entry-1", DateTime.UtcNow);
        List<RssSubscription> subscriptions =
        [
            new() { ChannelDiscordId = 1 },
            new() { ChannelDiscordId = 2 }
        ];
        int attempts = 0;

        Task<bool> SendAsync(RssSubscription _, string __)
        {
            attempts++;
            return Task.FromResult(attempts > subscriptions.Count);
        }

        bool firstSucceeded = await RssFeedJob.DispatchAsync(entry, subscriptions, SendAsync);
        bool secondSucceeded = await RssFeedJob.DispatchAsync(entry, subscriptions, SendAsync);

        Assert.False(firstSucceeded);
        Assert.True(secondSucceeded);
        Assert.Equal(4, attempts);
    }

    [Fact]
    public async Task DispatchAsync_WhenEntryHasNoContent_TreatsItAsHandledWithoutSending()
    {
        RssFeedService.FeedEntry entry = new("entry-1", string.Empty, string.Empty, DateTime.UtcNow);
        bool sent = false;

        bool succeeded = await RssFeedJob.DispatchAsync(
            entry,
            [new RssSubscription()],
            (_, _) =>
            {
                sent = true;
                return Task.FromResult(true);
            });

        Assert.True(succeeded);
        Assert.False(sent);
    }
}