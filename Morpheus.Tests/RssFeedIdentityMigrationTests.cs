using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Jobs;
using Morpheus.Services;

namespace Morpheus.Tests;

public class RssFeedIdentityMigrationTests : IAsyncLifetime
{
    private const string FeedUrl = "https://example.com/feed";
    private const string ArticleUrl = "https://example.com/article";
    private const string AtomUrl = "https://example.com/article.atom";
    private static readonly DateTime OriginalSeenAt = new(2025, 7, 30, 10, 0, 0, DateTimeKind.Utc);
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private DB db = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new DB(new DbContextOptionsBuilder<DB>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("<id>   </id>")]
    public async Task LoadSeenEntriesAsync_WhenAtomFallbackChanges_MigratesPersistedIdentity(string idElement)
    {
        RssFeedService.FeedEntry entry = Assert.Single(RssFeedService.ParseEntries(XDocument.Parse($$"""
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry>
                {{idElement}}
                <link rel="alternate" type="application/atom+xml" href="{{AtomUrl}}" />
                <link rel="alternate" type="text/html" href="{{ArticleUrl}}" />
              </entry>
            </feed>
            """)));
        int historyId = await SeedAsync(AtomUrl);

        (HashSet<string> seen, bool migrated) = await RssFeedJob.LoadSeenEntriesAsync(db, FeedUrl, [entry]);

        Assert.True(migrated);
        Assert.Contains(entry.EntryId, seen);
        Assert.Equal(ArticleUrl, entry.EntryId);
        Assert.Equal(AtomUrl, entry.LegacyEntryId);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        RssSeenEntry stored = await db.RssSeenEntries.SingleAsync();
        Assert.Equal(historyId, stored.Id);
        Assert.Equal(ArticleUrl, stored.EntryId);
        Assert.Equal(OriginalSeenAt, stored.SeenAt);

        (seen, migrated) = await RssFeedJob.LoadSeenEntriesAsync(db, FeedUrl, [entry]);
        Assert.False(migrated);
        Assert.Contains(entry.EntryId, seen);
    }

    [Fact]
    public async Task LoadSeenEntriesAsync_WhenRdfIdentityReplacesLink_RemainsSeenAfterLinkChanges()
    {
        const string stableId = "https://example.com/articles/123";
        RssFeedService.FeedEntry entry = ParseRssOne(stableId, ArticleUrl);
        int historyId = await SeedAsync(ArticleUrl);

        (HashSet<string> seen, bool migrated) = await RssFeedJob.LoadSeenEntriesAsync(db, FeedUrl, [entry]);

        Assert.True(migrated);
        Assert.Contains(stableId, seen);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        RssSeenEntry stored = await db.RssSeenEntries.SingleAsync();
        Assert.Equal(historyId, stored.Id);
        Assert.Equal(stableId, stored.EntryId);
        Assert.Equal(OriginalSeenAt, stored.SeenAt);

        RssFeedService.FeedEntry changed = ParseRssOne(stableId, "https://example.com/renamed");
        (seen, migrated) = await RssFeedJob.LoadSeenEntriesAsync(db, FeedUrl, [changed]);
        Assert.False(migrated);
        Assert.Contains(changed.EntryId, seen);
    }

    [Fact]
    public async Task LoadSeenEntriesAsync_AfterMigration_DoesNotSuppressNewRdfIdentitySharingOldLink()
    {
        RssFeedService.FeedEntry original = ParseRssOne("https://example.com/articles/123", ArticleUrl);
        await SeedAsync(ArticleUrl);
        await RssFeedJob.LoadSeenEntriesAsync(db, FeedUrl, [original]);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        RssFeedService.FeedEntry next = ParseRssOne("https://example.com/articles/456", ArticleUrl);

        (HashSet<string> seen, bool migrated) = await RssFeedJob.LoadSeenEntriesAsync(db, FeedUrl, [original, next]);

        Assert.False(migrated);
        Assert.Contains(original.EntryId, seen);
        Assert.DoesNotContain(next.EntryId, seen);
    }

    [Fact]
    public async Task LoadSeenEntriesAsync_DoesNotReplaceAnotherCurrentEntryIdentity()
    {
        RssFeedService.FeedEntry original = ParseRssOne(ArticleUrl, ArticleUrl);
        RssFeedService.FeedEntry next = ParseRssOne("https://example.com/articles/456", ArticleUrl);
        await SeedAsync(original.EntryId);

        (HashSet<string> seen, bool migrated) = await RssFeedJob.LoadSeenEntriesAsync(db, FeedUrl, [next, original]);

        Assert.False(migrated);
        Assert.Contains(original.EntryId, seen);
        Assert.DoesNotContain(next.EntryId, seen);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoadSeenEntriesAsync_WithExplicitAtomIdOrRssGuid_DoesNotUseLinkAsAlias(bool atom)
    {
        string xml = atom
            ? $$"""
              <feed xmlns="http://www.w3.org/2005/Atom"><entry>
                <id>new-stable-id</id>
                <link rel="alternate" type="application/atom+xml" href="{{AtomUrl}}" />
                <link rel="alternate" type="text/html" href="{{ArticleUrl}}" />
              </entry></feed>
              """
            : $$"""
              <rss version="2.0"><channel><item>
                <guid>new-stable-id</guid>
                <link>{{ArticleUrl}}</link>
              </item></channel></rss>
              """;
        RssFeedService.FeedEntry entry = Assert.Single(RssFeedService.ParseEntries(XDocument.Parse(xml)));
        await SeedAsync(ArticleUrl);
        await SeedAsync(AtomUrl);

        (HashSet<string> seen, bool migrated) = await RssFeedJob.LoadSeenEntriesAsync(db, FeedUrl, [entry]);

        Assert.Null(entry.LegacyEntryId);
        Assert.False(migrated);
        Assert.DoesNotContain(entry.EntryId, seen);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task LoadSeenEntriesAsync_WithLegacyIdOnlyInAnotherFeed_DoesNotSuppressEntry()
    {
        RssFeedService.FeedEntry entry = ParseRssOne("https://example.com/articles/123", ArticleUrl);
        await SeedAsync(ArticleUrl, "https://example.com/another-feed");

        (HashSet<string> seen, bool migrated) = await RssFeedJob.LoadSeenEntriesAsync(db, FeedUrl, [entry]);

        Assert.False(migrated);
        Assert.Empty(seen);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task LoadSeenEntriesAsync_WithoutHistory_DoesNotMarkEntriesSeen()
    {
        RssFeedService.FeedEntry entry = ParseRssOne("https://example.com/articles/123", ArticleUrl);

        (HashSet<string> seen, bool migrated) = await RssFeedJob.LoadSeenEntriesAsync(db, FeedUrl, [entry]);

        Assert.False(migrated);
        Assert.Empty(seen);
        Assert.False(await RssFeedJob.HasFeedHistoryAsync(db, FeedUrl));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    private async Task<int> SeedAsync(string entryId, string feedUrl = FeedUrl)
    {
        RssSeenEntry entry = new() { FeedUrl = feedUrl, EntryId = entryId, SeenAt = OriginalSeenAt };
        db.RssSeenEntries.Add(entry);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return entry.Id;
    }

    private static RssFeedService.FeedEntry ParseRssOne(string about, string link) =>
        Assert.Single(RssFeedService.ParseEntries(XDocument.Parse($$"""
            <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                     xmlns="http://purl.org/rss/1.0/">
              <item rdf:about="{{about}}">
                <title>Post</title><link>{{link}}</link>
              </item>
            </rdf:RDF>
            """)));
}
