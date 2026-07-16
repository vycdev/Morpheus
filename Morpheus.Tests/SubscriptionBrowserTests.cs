using Morpheus.Utilities;

namespace Morpheus.Tests;

public class SubscriptionBrowserTests
{
    [Fact]
    public void GetPage_PaginatesLargeCollectionsWithoutDroppingItems()
    {
        SubscriptionBrowserItem[] items = Enumerable.Range(1, 23)
            .Select(index => new SubscriptionBrowserItem(SubscriptionFeedType.Youtube, $"Channel {index}", (ulong)index))
            .ToArray();

        SubscriptionBrowserPage first = SubscriptionBrowser.GetPage(items, SubscriptionFeedType.All, 0);
        SubscriptionBrowserPage last = SubscriptionBrowser.GetPage(items, SubscriptionFeedType.All, 2);

        Assert.Equal(3, first.TotalPages);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal((1, 10), (first.FirstItemNumber, first.LastItemNumber));
        Assert.Equal(3, last.Items.Count);
        Assert.Equal((21, 23), (last.FirstItemNumber, last.LastItemNumber));

        string[] pagedNames = Enumerable.Range(0, first.TotalPages)
            .SelectMany(page => SubscriptionBrowser.GetPage(items, SubscriptionFeedType.All, page).Items)
            .Select(item => item.Name)
            .ToArray();
        Assert.Equal(items.Select(item => item.Name), pagedNames);
    }

    [Fact]
    public void GetPage_FiltersByFeedTypeAndClampsPageIndex()
    {
        SubscriptionBrowserItem[] items =
        [
            new(SubscriptionFeedType.Youtube, "Video", 1),
            new(SubscriptionFeedType.Rss, "Blog", 2),
            new(SubscriptionFeedType.Rss, "Releases", 3)
        ];

        SubscriptionBrowserPage page = SubscriptionBrowser.GetPage(items, SubscriptionFeedType.Rss, 99);

        Assert.Equal(0, page.PageIndex);
        Assert.Equal(2, page.TotalItems);
        Assert.All(page.Items, item => Assert.Equal(SubscriptionFeedType.Rss, item.FeedType));
    }

    [Fact]
    public void Counts_IncludeAllCategories()
    {
        SubscriptionBrowserItem[] items =
        [
            new(SubscriptionFeedType.Youtube, "Video", 1),
            new(SubscriptionFeedType.Rss, "Blog", 2),
            new(SubscriptionFeedType.Twitch, "Streamer", 3),
            new(SubscriptionFeedType.Xkcd, "xkcd", 4)
        ];

        Assert.Equal(4, SubscriptionBrowser.Count(items, SubscriptionFeedType.All));
        Assert.Equal(1, SubscriptionBrowser.Count(items, SubscriptionFeedType.Youtube));
        Assert.Equal(1, SubscriptionBrowser.Count(items, SubscriptionFeedType.Rss));
        Assert.Equal(1, SubscriptionBrowser.Count(items, SubscriptionFeedType.Twitch));
        Assert.Equal(1, SubscriptionBrowser.Count(items, SubscriptionFeedType.Xkcd));
    }
}
