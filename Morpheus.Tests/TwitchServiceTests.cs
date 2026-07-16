using Morpheus.Services;

namespace Morpheus.Tests;

public class TwitchServiceTests
{
    [Theory]
    [InlineData(3600, 3540)]
    [InlineData(60, 0)]
    [InlineData(30, 0)]
    [InlineData(-1, 0)]
    public void CalculateTokenCacheDuration_NeverCachesBeyondExpiry(int expiresInSeconds, int expectedSeconds)
    {
        TimeSpan duration = TwitchService.CalculateTokenCacheDuration(expiresInSeconds);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), duration);
    }
}
