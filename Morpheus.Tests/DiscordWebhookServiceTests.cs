using Morpheus.Services;

namespace Morpheus.Tests;

public class DiscordWebhookServiceTests
{
    [Fact]
    public void ClampUsername_KeepsWebhookOverrideWithinDiscordLimit()
    {
        string result = DiscordWebhookService.ClampUsername(new string('x', 80) + "tail");

        Assert.Equal(DiscordWebhookService.MaxUsernameLength, result.Length);
        Assert.Equal(new string('x', 79) + "…", result);
    }

    [Fact]
    public void ClampUsername_DoesNotSplitSurrogatePairs()
    {
        string result = DiscordWebhookService.ClampUsername(new string('x', 78) + "😀tail");

        Assert.Equal(new string('x', 78) + "…", result);
        Assert.DoesNotContain(result, char.IsSurrogate);
    }

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
