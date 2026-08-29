using Morpheus.Services;
using System.Xml.Linq;

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
    public void ParseEntries_ParsesNamespacedRssItems()
    {
        XDocument document = XDocument.Parse("""
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                     xmlns="http://purl.org/rss/1.0/"
                     xmlns:dc="http://purl.org/dc/elements/1.1/">
              <channel rdf:about="https://example.com/feed">
                <title>Example feed</title>
              </channel>
              <item rdf:about="https://example.com/posts/1">
                <title>Namespaced item</title>
                <link>https://example.com/posts/1</link>
                <dc:date>2025-07-30T12:00:00+02:00</dc:date>
              </item>
            </rdf:RDF>
            """);

        RssFeedService.FeedEntry entry = Assert.Single(RssFeedService.ParseRssEntries(document));

        Assert.Equal("https://example.com/posts/1", entry.EntryId);
        Assert.Equal("Namespaced item", entry.Title);
        Assert.Equal("https://example.com/posts/1", entry.Link);
        Assert.Equal(new DateTime(2025, 7, 30, 10, 0, 0, DateTimeKind.Utc), entry.Published);
    }

    [Fact]
    public void ParseEntries_WhenLinkUsesHrefAttribute_ReturnsHref()
    {
        XDocument document = XDocument.Parse("""
            <rss version="2.0">
              <channel>
                <item>
                  <guid>entry-1</guid>
                  <title>Entry</title>
                  <link href="https://example.com/entry-1" />
                </item>
              </channel>
            </rss>
            """);

        RssFeedService.FeedEntry entry = Assert.Single(RssFeedService.ParseRssEntries(document));

        Assert.Equal("https://example.com/entry-1", entry.Link);
    }

    [Fact]
    public void ParseEntries_WhenGuidIsBlank_UsesLinkAsEntryId()
    {
        XDocument document = XDocument.Parse("""
            <rss version="2.0">
              <channel>
                <item>
                  <guid>   </guid>
                  <title>Entry with blank guid</title>
                  <link>https://example.com/posts/1</link>
                  <pubDate>Wed, 30 Jul 2025 10:00:00 GMT</pubDate>
                </item>
              </channel>
            </rss>
            """);

        RssFeedService.FeedEntry entry = Assert.Single(RssFeedService.ParseRssEntries(document));

        Assert.Equal("https://example.com/posts/1", entry.EntryId);
    }

    [Fact]
    public void ParseEntries_TrimsWhitespaceAroundLinks()
    {
        XDocument document = XDocument.Parse("""
            <rss version="2.0">
              <channel>
                <item>
                  <guid>entry-1</guid>
                  <title>Entry</title>
                  <link>
                    https://example.com/posts/1
                  </link>
                </item>
              </channel>
            </rss>
            """);

        RssFeedService.FeedEntry entry = Assert.Single(RssFeedService.ParseRssEntries(document));

        Assert.Equal("https://example.com/posts/1", entry.Link);
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
