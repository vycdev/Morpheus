using Morpheus.Services;

namespace Morpheus.Tests;

public class RssFeedServiceTests
{
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
