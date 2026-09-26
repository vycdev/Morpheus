using System.Net;
using Morpheus.Utilities;

namespace Morpheus.Tests;

public class YoutubeUtilsTests
{
    private const string ChannelId = "UCabcdefghijklmnopqrstuv";

    [Theory]
    [InlineData("https://example.com/@channel")]
    [InlineData("http://127.0.0.1/private")]
    [InlineData("https://www.youtube.com.example.com/@channel")]
    [InlineData("https://evil.youtube.com/@channel")]
    [InlineData("https://example.com/channel/UCabcdefghijklmnopqrstuv")]
    [InlineData("example.com/channel/UCabcdefghijklmnopqrstuv")]
    [InlineData("//example.com/channel/UCabcdefghijklmnopqrstuv")]
    [InlineData("example.com/@channel")]
    [InlineData("https://www.youtube.com/redirect?q=http://127.0.0.1/private")]
    public async Task ResolveChannelIdAsync_DoesNotRequestNonYoutubeUrls(string input)
    {
        RecordingHandler handler = new(_ => throw new InvalidOperationException("Unexpected HTTP request."));
        using HttpClient httpClient = new(handler);

        string? result = await YoutubeUtils.ResolveChannelIdAsync(httpClient, input);

        Assert.Null(result);
        Assert.Empty(handler.RequestedUris);
    }

    [Fact]
    public async Task GetChannelAvatarAsync_WhenCallerCancels_PropagatesCancellation()
    {
        RecordingHandler handler = new(_ => throw new InvalidOperationException("Unexpected HTTP request."));
        using HttpClient httpClient = new(handler);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            YoutubeUtils.GetChannelAvatarAsync(httpClient, "UC123", cts.Token));
    }

    [Theory]
    [InlineData("https://www.youtube.com/@channel", "/@channel", "?cbrd=1&ucbcb=1")]
    [InlineData("youtube.com/user/channel", "/user/channel", "?cbrd=1&ucbcb=1")]
    [InlineData("https://m.youtube.com/c/channel?feature=share", "/c/channel", "?cbrd=1&ucbcb=1")]
    [InlineData("https://music.youtube.com/@channel", "/@channel", "?cbrd=1&ucbcb=1")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=tracking", "/watch", "?v=dQw4w9WgXcQ")]
    [InlineData("@channel", "/@channel", "?cbrd=1&ucbcb=1")]
    public async Task ResolveChannelIdAsync_RequestsCanonicalYoutubeUrls(
        string input,
        string expectedPath,
        string expectedQuery)
    {
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"\"channelId\":\"{ChannelId}\"")
        });
        using HttpClient httpClient = new(handler);

        string? result = await YoutubeUtils.ResolveChannelIdAsync(httpClient, input);

        Assert.Equal(ChannelId, result);
        Uri requestedUri = Assert.Single(handler.RequestedUris);
        Assert.Equal("www.youtube.com", requestedUri.Host);
        Assert.Equal(expectedPath, requestedUri.AbsolutePath);
        Assert.Equal(expectedQuery, requestedUri.Query);
    }

    [Theory]
    [InlineData("https://www.youtube.com/channel/UCabcdefghijklmnopqrstuv")]
    [InlineData("https://music.youtube.com/channel/UCabcdefghijklmnopqrstuv")]
    [InlineData("youtube.com/channel/UCabcdefghijklmnopqrstuv")]
    [InlineData("/channel/UCabcdefghijklmnopqrstuv")]
    [InlineData("https://www.youtube.com/channel/UCabcdefghijklmnopqrstuv/videos")]
    public async Task ResolveChannelIdAsync_ReturnsIdsFromYoutubeChannelPathsWithoutRequesting(string input)
    {
        RecordingHandler handler = new(_ => throw new InvalidOperationException("Unexpected HTTP request."));
        using HttpClient httpClient = new(handler);

        string? result = await YoutubeUtils.ResolveChannelIdAsync(httpClient, input);

        Assert.Equal(ChannelId, result);
        Assert.Empty(handler.RequestedUris);
    }

    [Theory]
    [InlineData("https://www.youtube.com/channel/UCabcdefghijklmnopqrstuvx")]
    [InlineData("https://www.youtube.com/channel/UCabcdefghijklmnopqrstuv_")]
    [InlineData("https://www.youtube.com/channel/UCabcdefghijklmnopqrstuv!junk")]
    [InlineData("https://www.youtube.com/channel/UCabcdefghijklmnopqrstuv.extra")]
    [InlineData("https://www.youtube.com/channel/UCabcdefghijklmnopqrstuv%41")]
    public async Task ResolveChannelIdAsync_DoesNotTruncateOverlongChannelIds(string input)
    {
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using HttpClient httpClient = new(handler);

        string? result = await YoutubeUtils.ResolveChannelIdAsync(httpClient, input);

        Assert.Null(result);
        Assert.Single(handler.RequestedUris);
    }

    [Fact]
    public async Task ResolveChannelIdAsync_ReturnsIdFromCanonicalChannelUrl()
    {
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"<link rel=\"canonical\" href=\"https://www.youtube.com/channel/{ChannelId}\">")
        });
        using HttpClient httpClient = new(handler);

        string? result = await YoutubeUtils.ResolveChannelIdAsync(httpClient, "@channel");

        Assert.Equal(ChannelId, result);
        Assert.Single(handler.RequestedUris);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<Uri> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<HttpResponseMessage>(cancellationToken);

            if (request.RequestUri != null)
                RequestedUris.Add(request.RequestUri);

            return Task.FromResult(responseFactory(request));
        }
    }
}
