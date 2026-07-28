using Morpheus.Database.Models;
using Morpheus.Jobs;
using Morpheus.Services;

namespace Morpheus.Tests;

public class RssFeedJobTests
{
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