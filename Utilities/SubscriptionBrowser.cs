namespace Morpheus.Utilities;

internal enum SubscriptionFeedType
{
    All,
    Youtube,
    Rss,
    Twitch,
    Xkcd
}

internal sealed record SubscriptionBrowserItem(
    SubscriptionFeedType FeedType,
    string Name,
    ulong ChannelId,
    string? SourceUrl = null);

internal sealed record SubscriptionBrowserPage(
    SubscriptionFeedType Filter,
    IReadOnlyList<SubscriptionBrowserItem> Items,
    int PageIndex,
    int TotalPages,
    int TotalItems,
    int FirstItemNumber,
    int LastItemNumber);

internal static class SubscriptionBrowser
{
    internal const int PageSize = 10;

    internal static SubscriptionBrowserPage GetPage(
        IReadOnlyList<SubscriptionBrowserItem> allItems,
        SubscriptionFeedType filter,
        int requestedPage)
    {
        SubscriptionBrowserItem[] filtered = allItems
            .Where(item => filter == SubscriptionFeedType.All || item.FeedType == filter)
            .ToArray();

        int totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Length / (double)PageSize));
        int pageIndex = Math.Clamp(requestedPage, 0, totalPages - 1);
        SubscriptionBrowserItem[] pageItems = filtered
            .Skip(pageIndex * PageSize)
            .Take(PageSize)
            .ToArray();

        int first = filtered.Length == 0 ? 0 : pageIndex * PageSize + 1;
        int last = filtered.Length == 0 ? 0 : first + pageItems.Length - 1;
        return new SubscriptionBrowserPage(filter, pageItems, pageIndex, totalPages, filtered.Length, first, last);
    }

    internal static int Count(IReadOnlyList<SubscriptionBrowserItem> items, SubscriptionFeedType feedType) =>
        feedType == SubscriptionFeedType.All
            ? items.Count
            : items.Count(item => item.FeedType == feedType);

    internal static string DisplayName(SubscriptionFeedType feedType) => feedType switch
    {
        SubscriptionFeedType.All => "All",
        SubscriptionFeedType.Youtube => "YouTube",
        SubscriptionFeedType.Rss => "RSS",
        SubscriptionFeedType.Twitch => "Twitch",
        SubscriptionFeedType.Xkcd => "xkcd",
        _ => feedType.ToString()
    };
}
