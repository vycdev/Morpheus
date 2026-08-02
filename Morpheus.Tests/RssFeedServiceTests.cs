using Morpheus.Services;

namespace Morpheus.Tests;

public class RssFeedServiceTests
{
    [Theory]
    [InlineData("Wed, 30 Jul 2025 12:00:00 +0200", 2025, 7, 30, 10, 0, 0)]
    [InlineData("2025-07-30T12:00:00+02:00", 2025, 7, 30, 10, 0, 0)]
    [InlineData("Tue, 01 Jul 2025 00:30:00 -0200", 2025, 7, 1, 2, 30, 0)]
    [InlineData("Wed, 30 Jul 2025 10:00:00 GMT", 2025, 7, 30, 10, 0, 0)]
    public void ParsePublished_NormalizesFeedDatesToUtc(
        string value,
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second)
    {
        DateTime published = RssFeedService.ParsePublished(value);

        Assert.Equal(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc), published);
        Assert.Equal(DateTimeKind.Utc, published.Kind);
    }

    [Fact]
    public void ParsePublished_WhenValueIsInvalid_ReturnsMinimumValue()
    {
        Assert.Equal(DateTime.MinValue, RssFeedService.ParsePublished("not-a-date"));
    }

    [Fact]
    public async Task FetchAsync_WhenCallerCancels_PropagatesCancellation()
    {
        RssFeedService service = new(new LogsService(new LogQueue()));
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.FetchAsync("https://example.com/feed.xml", cts.Token));
    }
}
