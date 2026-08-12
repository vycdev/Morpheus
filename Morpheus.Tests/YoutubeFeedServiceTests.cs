using Morpheus.Services;

namespace Morpheus.Tests;

public class YoutubeFeedServiceTests
{
    [Theory]
    [InlineData("2025-07-30T12:00:00+02:00", 10)]
    [InlineData("2025-07-30T10:00:00Z", 10)]
    public void ParsePublished_NormalizesAtomTimestampsToUtc(string value, int expectedHour)
    {
        DateTime published = YoutubeFeedService.ParsePublished(value);

        Assert.Equal(new DateTime(2025, 7, 30, expectedHour, 0, 0, DateTimeKind.Utc), published);
        Assert.Equal(DateTimeKind.Utc, published.Kind);
    }

    [Fact]
    public void ParsePublished_WhenValueIsInvalid_ReturnsMinimumValue()
    {
        Assert.Equal(DateTime.MinValue, YoutubeFeedService.ParsePublished("not-a-date"));
    }

    [Fact]
    public async Task FetchFeedAsync_WhenCallerCancels_PropagatesCancellation()
    {
        YoutubeFeedService service = new(new LogsService(new LogQueue()));
        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.FetchFeedAsync("channel-id", cts.Token));
    }
}
