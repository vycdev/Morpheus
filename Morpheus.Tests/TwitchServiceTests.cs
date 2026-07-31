using System.Net;
using System.Text;
using Morpheus.Services;

namespace Morpheus.Tests;

public class TwitchServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetUserAsync_PropagatesCallerCancellation(bool cancelDuringTokenRequest)
    {
        CancellationHandler handler = new(cancelDuringTokenRequest);
        using HttpClient httpClient = new(handler);
        TwitchService service = new(new LogsService(new LogQueue()), httpClient, "test-client", "test-secret");
        using CancellationTokenSource cancellation = new();

        Task<TwitchService.TwitchUser?> request = service.GetUserAsync("streamer", cancellation.Token);
        await handler.RequestStarted;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

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

    private sealed class CancellationHandler(bool blockTokenRequest) : HttpMessageHandler
    {
        private readonly TaskCompletionSource requestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RequestStarted => requestStarted.Task;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == "id.twitch.tv" && !blockTokenRequest)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"token\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
                };
            }

            requestStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The canceled Twitch request unexpectedly completed.");
        }
    }
}
