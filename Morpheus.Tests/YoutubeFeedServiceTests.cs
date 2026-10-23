using Morpheus.Services;
using System.Xml.Linq;

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

    [Theory]
    [InlineData("")]
    [InlineData("<published>  </published>")]
    public void ParseEntries_WhenPublishedIsMissingOrBlank_UsesUpdatedDate(string publishedElement)
    {
        XDocument document = XDocument.Parse($"""
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:yt="http://www.youtube.com/xml/schemas/2015">
              <entry>
                <yt:videoId>video-id</yt:videoId>
                {publishedElement}
                <updated>2025-07-30T10:00:00Z</updated>
              </entry>
            </feed>
            """);

        YoutubeFeedService.VideoEntry entry = Assert.Single(YoutubeFeedService.ParseEntries(document));
        Assert.Equal(new DateTime(2025, 7, 30, 10, 0, 0, DateTimeKind.Utc), entry.Published);
    }

    [Fact]
    public void ParseEntries_WhenPublishedIsMalformed_UsesValidUpdatedDate()
    {
        XDocument document = XDocument.Parse("""
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:yt="http://www.youtube.com/xml/schemas/2015">
              <entry>
                <yt:videoId>video-id</yt:videoId>
                <published>not-a-date</published>
                <updated>2025-07-30T10:00:00Z</updated>
              </entry>
            </feed>
            """);

        YoutubeFeedService.VideoEntry entry = Assert.Single(YoutubeFeedService.ParseEntries(document));
        Assert.Equal(new DateTime(2025, 7, 30, 10, 0, 0, DateTimeKind.Utc), entry.Published);
    }

    [Theory]
    [InlineData("2025-07-29T10:00:00Z", "2025-07-29T10:00:00Z")]
    [InlineData("not-a-date", null)]
    public void ParseEntries_WhenUpdatedDateIsInvalid_PrefersValidPublishedOrReturnsMinimum(
        string publishedValue, string? expectedValue)
    {
        XDocument document = XDocument.Parse($"""
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:yt="http://www.youtube.com/xml/schemas/2015">
              <entry>
                <yt:videoId>video-id</yt:videoId>
                <published>{publishedValue}</published>
                <updated>not-a-date</updated>
              </entry>
            </feed>
            """);

        YoutubeFeedService.VideoEntry entry = Assert.Single(YoutubeFeedService.ParseEntries(document));
        Assert.Equal(YoutubeFeedService.ParsePublished(expectedValue ?? string.Empty), entry.Published);
    }

    [Fact]
    public void ParseEntries_WhenBothDatesAreValid_PrefersPublished()
    {
        XDocument document = XDocument.Parse("""
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:yt="http://www.youtube.com/xml/schemas/2015">
              <entry>
                <yt:videoId>video-id</yt:videoId>
                <published>2025-07-29T10:00:00Z</published>
                <updated>2025-07-30T10:00:00Z</updated>
              </entry>
            </feed>
            """);

        YoutubeFeedService.VideoEntry entry = Assert.Single(YoutubeFeedService.ParseEntries(document));
        Assert.Equal(new DateTime(2025, 7, 29, 10, 0, 0, DateTimeKind.Utc), entry.Published);
    }

    [Fact]
    public void ParseEntries_TrimsWhitespaceAroundLinks()
    {
        XDocument document = XDocument.Parse("""
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:yt="http://www.youtube.com/xml/schemas/2015">
              <entry>
                <yt:videoId>video-id</yt:videoId>
                <title>Video title</title>
                <link href="&#xA;  https://www.youtube.com/watch?v=video-id  &#xA;" />
                <published>2025-07-30T10:00:00Z</published>
              </entry>
            </feed>
            """);

        YoutubeFeedService.VideoEntry entry = Assert.Single(YoutubeFeedService.ParseEntries(document));

        Assert.Equal("https://www.youtube.com/watch?v=video-id", entry.Link);
    }

    [Fact]
    public void ParseEntries_PrefersAlternateLinkOverFeedMetadataLinks()
    {
        XDocument document = XDocument.Parse("""
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:yt="http://www.youtube.com/xml/schemas/2015">
              <entry>
                <yt:videoId>video-id</yt:videoId>
                <link rel="self" href="https://www.youtube.com/feeds/videos.xml?video_id=video-id" />
                <link rel="alternate" href="https://www.youtube.com/watch?v=video-id" />
              </entry>
            </feed>
            """);

        YoutubeFeedService.VideoEntry entry = Assert.Single(YoutubeFeedService.ParseEntries(document));
        Assert.Equal("https://www.youtube.com/watch?v=video-id", entry.Link);
    }

    [Theory]
    [InlineData("<link rel=\"self\" href=\"https://www.youtube.com/feeds/videos.xml?video_id=video-id\" />")]
    [InlineData("<link rel=\"alternate\" href=\"  \" /><link rel=\"self\" href=\"https://www.youtube.com/feeds/videos.xml?video_id=video-id\" />")]
    public void ParseEntries_WhenOnlyMetadataOrBlankAlternateLinksExist_UsesCanonicalVideoUrl(string linkElements)
    {
        XDocument document = XDocument.Parse($"""
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:yt="http://www.youtube.com/xml/schemas/2015">
              <entry>
                <yt:videoId>video-id</yt:videoId>
                {linkElements}
              </entry>
            </feed>
            """);

        YoutubeFeedService.VideoEntry entry = Assert.Single(YoutubeFeedService.ParseEntries(document));
        Assert.Equal("https://www.youtube.com/watch?v=video-id", entry.Link);
    }

    [Fact]
    public void ParseEntries_TrimsWhitespaceAroundVideoIds()
    {
        XDocument document = XDocument.Parse("""
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:yt="http://www.youtube.com/xml/schemas/2015">
              <entry>
                <yt:videoId>
                  video-id
                </yt:videoId>
                <title>Video title</title>
                <published>2025-07-30T10:00:00Z</published>
              </entry>
            </feed>
            """);

        YoutubeFeedService.VideoEntry entry = Assert.Single(YoutubeFeedService.ParseEntries(document));

        Assert.Equal("video-id", entry.VideoId);
        Assert.Equal("https://www.youtube.com/watch?v=video-id", entry.Link);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<link />")]
    [InlineData("<link href=\"\" />")]
    [InlineData("<link href=\"  \" />")]
    [InlineData("<link href=\"&#x9;&#xA;&#xD;\" />")]
    public void ParseEntries_WhenLinkIsMissingOrBlank_UsesCanonicalVideoUrl(string linkElement)
    {
        XDocument document = XDocument.Parse($"""
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:yt="http://www.youtube.com/xml/schemas/2015">
              <entry>
                <yt:videoId>video-id</yt:videoId>
                <title>Video title</title>
                {linkElement}
                <published>2025-07-30T10:00:00Z</published>
              </entry>
            </feed>
            """);

        YoutubeFeedService.VideoEntry entry = Assert.Single(YoutubeFeedService.ParseEntries(document));

        Assert.Equal("https://www.youtube.com/watch?v=video-id", entry.Link);
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
