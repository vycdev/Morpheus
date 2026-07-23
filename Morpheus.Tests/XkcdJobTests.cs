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
}