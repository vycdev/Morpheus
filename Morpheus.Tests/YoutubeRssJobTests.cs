using Morpheus.Database.Models;
using Morpheus.Jobs;

namespace Morpheus.Tests;

public class YoutubeRssJobTests
{
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
}
