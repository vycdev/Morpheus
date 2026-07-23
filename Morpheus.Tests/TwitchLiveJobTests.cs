using Morpheus.Database.Models;
using Morpheus.Jobs;
using Morpheus.Services;

namespace Morpheus.Tests;

public class TwitchLiveJobTests
{
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
}
