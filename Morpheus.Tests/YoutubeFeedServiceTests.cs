using Morpheus.Services;

namespace Morpheus.Tests;

public class YoutubeFeedServiceTests
{
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
