using Morpheus.Services;

namespace Morpheus.Tests;

public class YoutubeFeedServiceTests
{
    [Fact]
    public void ParseAtomFeed_PreservesLegacyRssBehavior()
    {
        const string xml = """
            <feed xmlns="http://www.w3.org/2005/Atom" xmlns:yt="http://www.youtube.com/xml/schemas/2015">
              <title>Example Channel</title>
              <entry>
                <yt:videoId>abcdefghijk</yt:videoId>
                <title>Example video</title>
                <link rel="alternate" href="https://www.youtube.com/watch?v=abcdefghijk" />
                <published>2026-07-15T12:00:00Z</published>
              </entry>
            </feed>
            """;

        (string? channelTitle, IReadOnlyList<YoutubeFeedService.VideoEntry> entries) =
            YoutubeFeedService.ParseAtomFeed(xml);

        Assert.Equal("Example Channel", channelTitle);
        YoutubeFeedService.VideoEntry entry = Assert.Single(entries);
        Assert.Equal("abcdefghijk", entry.VideoId);
        Assert.Equal("Example video", entry.Title);
    }

    [Fact]
    public void ParseUploadsPage_ReadsCurrentLockupViewModelShape()
    {
        const string html = """
            <html><script>var ytInitialData = {
              "contents": [
                {"playlistHeaderRenderer":{"ownerText":{"runs":[{"text":"Example Channel"}]}}},
                {"lockupViewModel":{
                  "metadata":{"lockupMetadataViewModel":{"title":{"content":"Newest video"}}},
                  "contentId":"abcdefghijk",
                  "contentType":"LOCKUP_CONTENT_TYPE_VIDEO"
                }},
                {"lockupViewModel":{
                  "metadata":{"lockupMetadataViewModel":{"title":{"content":"Older video"}}},
                  "contentId":"lmnopqrstuv",
                  "contentType":"LOCKUP_CONTENT_TYPE_VIDEO"
                }}
              ]
            };</script></html>
            """;

        (string? channelTitle, IReadOnlyList<YoutubeFeedService.VideoEntry> entries) =
            YoutubeFeedService.ParseUploadsPage(html);

        Assert.Equal("Example Channel", channelTitle);
        Assert.Collection(entries,
            newest =>
            {
                Assert.Equal("abcdefghijk", newest.VideoId);
                Assert.Equal("Newest video", newest.Title);
                Assert.Equal("https://www.youtube.com/watch?v=abcdefghijk", newest.Link);
            },
            older => Assert.Equal("lmnopqrstuv", older.VideoId));
        Assert.True(entries[0].Published > entries[1].Published);
    }

    [Fact]
    public void ParseUploadsPage_DeduplicatesRepeatedVideoModels()
    {
        const string html = """
            <script>var ytInitialData = {
              "playlistHeaderRenderer":{"ownerText":{"runs":[{"text":"Channel"}]}},
              "items":[
                {"lockupViewModel":{"contentId":"abcdefghijk","contentType":"LOCKUP_CONTENT_TYPE_VIDEO"}},
                {"lockupViewModel":{"contentId":"abcdefghijk","contentType":"LOCKUP_CONTENT_TYPE_VIDEO"}}
              ]
            };</script>
            """;

        (_, IReadOnlyList<YoutubeFeedService.VideoEntry> entries) = YoutubeFeedService.ParseUploadsPage(html);

        Assert.Single(entries);
    }

    [Fact]
    public void ParseUploadsPage_ReturnsEmptyForMissingInitialData()
    {
        (string? channelTitle, IReadOnlyList<YoutubeFeedService.VideoEntry> entries) =
            YoutubeFeedService.ParseUploadsPage("<html></html>");

        Assert.Null(channelTitle);
        Assert.Empty(entries);
    }
}
