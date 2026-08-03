using Morpheus.Services;

namespace Morpheus.Tests;

public class DiscordWebhookServiceTests
{
    [Fact]
    public async Task SendAsync_WhenCallerCancels_PropagatesCancellation()
    {
        DiscordWebhookService service = new(new LogsService(new LogQueue()));
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SendAsync(123, "token", "content", "username", null, cts.Token));
    }

    [Fact]
    public async Task CheckExistsAsync_WhenCallerCancels_PropagatesCancellation()
    {
        DiscordWebhookService service = new(new LogsService(new LogQueue()));
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CheckExistsAsync(123, "token", cts.Token));
    }
}
