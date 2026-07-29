using Morpheus.Database.Models;
using Morpheus.Jobs;

namespace Morpheus.Tests;

public class XkcdJobTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    public void ShouldFetchFeed_RequiresAtLeastOneSubscriber(int subscriptionCount, bool expected)
    {
        Assert.Equal(expected, XkcdJob.ShouldFetchFeed(subscriptionCount));
    }

    [Fact]
    public async Task DispatchAsync_WhenOneDeliveryFails_ReportsFailureAndAttemptsEverySubscriber()
    {
        List<XkcdSubscription> subscriptions =
        [
            new() { ChannelDiscordId = 1 },
            new() { ChannelDiscordId = 2 }
        ];
        List<ulong> attemptedChannels = [];

        bool succeeded = await XkcdJob.DispatchAsync(
            "https://xkcd.com/1/",
            subscriptions,
            (subscription, _) =>
            {
                attemptedChannels.Add(subscription.ChannelDiscordId);
                return Task.FromResult(subscription.ChannelDiscordId != 1);
            });

        Assert.False(succeeded);
        Assert.Equal([1UL, 2UL], attemptedChannels);
    }
}
