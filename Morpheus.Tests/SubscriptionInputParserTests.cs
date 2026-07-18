using Morpheus.Utilities;

namespace Morpheus.Tests;

public class SubscriptionInputParserTests
{
    [Fact]
    public void ParseSources_AcceptsWhitespaceCommasNewlinesAndTargetChannel()
    {
        SubscriptionInputParser.SourceList parsed = SubscriptionInputParser.ParseSources(
            "UC-one, UC-two\nUC-three <#123456789>");

        Assert.Equal(123456789UL, parsed.ChannelId);
        Assert.Equal(["UC-one", "UC-two", "UC-three"], parsed.Sources);
    }

    [Fact]
    public void ParseSources_DeduplicatesCaseInsensitively()
    {
        SubscriptionInputParser.SourceList parsed = SubscriptionInputParser.ParseSources("Streamer streamer STREAMER");

        Assert.Single(parsed.Sources);
        Assert.Equal("Streamer", parsed.Sources[0]);
    }

    [Fact]
    public void ParseRssSources_PreservesLegacySingleFeedDisplayName()
    {
        IReadOnlyList<SubscriptionInputParser.RssSource> parsed =
            SubscriptionInputParser.ParseRssSources("https://example.com/feed.xml Example feed");

        SubscriptionInputParser.RssSource source = Assert.Single(parsed);
        Assert.Equal("https://example.com/feed.xml", source.Url);
        Assert.Equal("Example feed", source.DisplayName);
    }

    [Fact]
    public void ParseRssSources_AcceptsSpaceSeparatedUrls()
    {
        IReadOnlyList<SubscriptionInputParser.RssSource> parsed = SubscriptionInputParser.ParseRssSources(
            "https://example.com/feed.xml https://github.com/example/project/releases.atom");

        Assert.Equal(2, parsed.Count);
        Assert.All(parsed, source => Assert.Null(source.DisplayName));
    }

    [Fact]
    public void ParseRssSources_AcceptsOneFeedPerLineWithOptionalNames()
    {
        IReadOnlyList<SubscriptionInputParser.RssSource> parsed = SubscriptionInputParser.ParseRssSources(
            "https://example.com/feed.xml | Example\nhttps://example.org/atom.xml Other feed");

        Assert.Collection(parsed,
            first => Assert.Equal("Example", first.DisplayName),
            second => Assert.Equal("Other feed", second.DisplayName));
    }

    [Fact]
    public void ParseRssSources_AcceptsCsvUrlAndDisplayName()
    {
        SubscriptionInputParser.RssSource parsed = Assert.Single(
            SubscriptionInputParser.ParseRssSources("https://example.com/feed.xml,Example feed"));

        Assert.Equal("https://example.com/feed.xml", parsed.Url);
        Assert.Equal("Example feed", parsed.DisplayName);
    }

    [Fact]
    public void ParseRssSources_PreservesUrlsWithCaseSensitivePaths()
    {
        IReadOnlyList<SubscriptionInputParser.RssSource> parsed = SubscriptionInputParser.ParseRssSources(
            "https://EXAMPLE.com/Feed.xml\nhttps://example.com/feed.xml\nhttps://example.com/Feed.xml");

        Assert.Equal(
            ["https://EXAMPLE.com/Feed.xml", "https://example.com/feed.xml"],
            parsed.Select(source => source.Url));
    }

    [Fact]
    public void ParseRssSources_PreservesCaseSensitivePathsInSpaceSeparatedUrls()
    {
        IReadOnlyList<SubscriptionInputParser.RssSource> parsed = SubscriptionInputParser.ParseRssSources(
            "https://example.com/Feed.xml https://example.com/feed.xml");

        Assert.Equal(
            ["https://example.com/Feed.xml", "https://example.com/feed.xml"],
            parsed.Select(source => source.Url));
    }
}
