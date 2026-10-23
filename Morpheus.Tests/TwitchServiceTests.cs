using Morpheus.Services;
using System.Net;
using System.Text;
using System.Text.Json;

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

    [Fact]
    public async Task GetUserAsync_DoesNotCacheTokenWhenExpiryUnderflows()
    {
        OverflowingExpiryHandler handler = new();
        using HttpClient httpClient = new(handler);
        TwitchService service = new(new LogsService(new LogQueue()), httpClient, "test-client", "test-secret");

        await service.GetUserAsync("streamer");
        await service.GetUserAsync("streamer");

        Assert.Equal(2, handler.TokenRequestCount);
    }

    [Fact]
    public async Task GetLiveStreamsAsync_IgnoresBlankAndTrimsDuplicateUserIds()
    {
        RecordingHandler handler = new();
        using HttpClient httpClient = new(handler);
        TwitchService service = new(new LogsService(new LogQueue()), httpClient, "test-client", "test-secret");

        IReadOnlyDictionary<string, TwitchService.TwitchStream> live =
            await service.GetLiveStreamsAsync([" ", " 42 ", string.Empty, "42"]);

        Assert.True(live.ContainsKey("42"));
        Assert.Single(handler.StreamRequests);
        Assert.Equal("https://api.twitch.tv/helix/streams?user_id=42", handler.StreamRequests[0]);
    }

    [Fact]
    public async Task GetLiveStreamsResultAsync_RequestsEnoughResultsForAllSubscribedUsers()
    {
        using HttpClient httpClient = new(new DefaultPageSizeHandler());
        TwitchService service = new(new LogsService(new LogQueue()), httpClient, "test-client", "test-secret");
        string[] userIds = Enumerable.Range(1, 25).Select(id => id.ToString()).ToArray();

        TwitchService.LiveStreamsResult result = await service.GetLiveStreamsResultAsync(userIds);

        Assert.True(result.Succeeded);
        Assert.Equal(25, result.Streams.Count);
        Assert.Contains("25", result.Streams.Keys);
    }

    private sealed class DefaultPageSizeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == "id.twitch.tv")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"token\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
                });

            // Helix defaults to 20 results per page, even if more user_id filters are supplied.
            string[] parameters = request.RequestUri!.Query.TrimStart('?').Split('&');
            string? first = parameters.FirstOrDefault(part => part.StartsWith("first=", StringComparison.Ordinal));
            int pageSize = first == null ? 20 : int.Parse(first["first=".Length..]);
            string[] ids = parameters.Where(part => part.StartsWith("user_id=", StringComparison.Ordinal))
                .Select(part => part["user_id=".Length..]).Take(pageSize).ToArray();
            string json = JsonSerializer.Serialize(new { data = ids.Select(id => new
            {
                id = $"stream-{id}", user_id = id, type = "live", title = "Live"
            }) });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> StreamRequests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == "id.twitch.tv")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"token\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
                });
            }

            StreamRequests.Add(request.RequestUri?.ToString() ?? string.Empty);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"id\":\"stream\",\"user_id\":\"42\",\"title\":\"Live\",\"type\":\"live\"}]}", Encoding.UTF8, "application/json")
            });
        }
    }

    [Fact]
    public async Task GetLiveStreamsResultAsync_WhenStreamsRequestFails_MarksResultAsUnknown()
    {
        using HttpClient httpClient = new(new StreamsFailureHandler());
        TwitchService service = new(new LogsService(new LogQueue()), httpClient, "test-client", "test-secret");

        TwitchService.LiveStreamsResult result = await service.GetLiveStreamsResultAsync(["123"]);

        Assert.False(result.Succeeded);
        Assert.Empty(result.Streams);
    }

    [Fact]
    public async Task GetLiveStreamsResultAsync_WhenLaterBatchFails_DoesNotReportPartialPollAsSuccessful()
    {
        LaterBatchFailureHandler handler = new();
        using HttpClient httpClient = new(handler);
        TwitchService service = new(new LogsService(new LogQueue()), httpClient, "test-client", "test-secret");
        string[] userIds = Enumerable.Range(1, 101)
            .Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

        TwitchService.LiveStreamsResult result = await service.GetLiveStreamsResultAsync(userIds);

        Assert.False(result.Succeeded);
        Assert.True(result.Streams.ContainsKey("1"));
        Assert.Equal(2, handler.StreamRequests.Count);
        Assert.Equal(100, handler.StreamRequests[0].Split("user_id=").Length - 1);
        Assert.EndsWith("?user_id=101", handler.StreamRequests[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetLiveStreamsResultAsync_CoalescesConcurrentTokenRefreshes(bool delaySecondUnauthorized)
    {
        ConcurrentUnauthorizedHandler handler = new(delaySecondUnauthorized);
        using HttpClient httpClient = new(handler);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        TwitchService service = new(new LogsService(new LogQueue()), httpClient, "test-client", "test-secret");

        TwitchService.LiveStreamsResult[] results = await Task.WhenAll(
            service.GetLiveStreamsResultAsync(["first"], timeout.Token),
            service.GetLiveStreamsResultAsync(["second"], timeout.Token));

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(2, handler.TokenRequestCount);
        Assert.Equal(4, handler.StreamRequestCount);
    }

    private sealed class OverflowingExpiryHandler : HttpMessageHandler
    {
        public int TokenRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == "id.twitch.tv")
            {
                TokenRequestCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $"{{\"access_token\":\"token-{TokenRequestCount}\",\"expires_in\":{int.MinValue}}}",
                        Encoding.UTF8,
                        "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"data\":[{\"id\":\"1\",\"login\":\"streamer\",\"display_name\":\"Streamer\"}]}",
                    Encoding.UTF8,
                    "application/json")
            });
        }
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

    private sealed class StreamsFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == "id.twitch.tv")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"token\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("temporarily unavailable")
            });
        }
    }

    private sealed class LaterBatchFailureHandler : HttpMessageHandler
    {
        public List<string> StreamRequests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == "id.twitch.tv")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"token\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
                });
            }

            StreamRequests.Add(request.RequestUri?.ToString() ?? string.Empty);
            return Task.FromResult(StreamRequests.Count == 1
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"data\":[{\"id\":\"stream-1\",\"user_id\":\"1\",\"title\":\"Live\",\"type\":\"live\"}]}",
                        Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

    private sealed class ConcurrentUnauthorizedHandler(bool delaySecondUnauthorized) : HttpMessageHandler
    {
        private readonly TaskCompletionSource bothInitialRequestsStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource replacementTokenUsed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int initialStreamRequestCount;
        private int tokenRequestCount;
        private int streamRequestCount;

        public int TokenRequestCount => tokenRequestCount;
        public int StreamRequestCount => streamRequestCount;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == "id.twitch.tv")
            {
                int requestNumber = Interlocked.Increment(ref tokenRequestCount);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $"{{\"access_token\":\"token-{requestNumber}\",\"expires_in\":3600}}",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            Interlocked.Increment(ref streamRequestCount);
            string? token = request.Headers.Authorization?.Parameter;
            if (token == "token-1")
            {
                if (Interlocked.Increment(ref initialStreamRequestCount) == 2)
                    bothInitialRequestsStarted.TrySetResult();

                await bothInitialRequestsStarted.Task.WaitAsync(cancellationToken);
                if (delaySecondUnauthorized && request.RequestUri?.Query == "?user_id=second")
                    await replacementTokenUsed.Task.WaitAsync(cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            replacementTokenUsed.TrySetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json")
            };
        }
    }
}
