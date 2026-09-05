using System.Xml.Linq;
using Discord;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

/// <summary>
/// Checks the xkcd RSS feed and posts any new comics to every subscribed channel through its
/// shared webhook, posting as "xkcd". Seen comics are recorded globally so they never repeat.
/// </summary>
[DisallowConcurrentExecution]
public class XkcdJob(DB db, DiscordWebhookService discordWebhook, LogsService logsService) : IJob
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    private const string XkcdUsername = "xkcd";
    internal const int MaxDeliveryAttempts = 24 * 7;
    // Public square avatar for the xkcd identity (data: URLs are rejected by Discord's avatar_url).
    private const string XkcdAvatarUrl = "https://pbs.twimg.com/profile_images/1488600831377252354/hEpPeSu0_400x400.jpg";

    internal record XkcdItem(string Title, string Link);

    internal static bool ShouldFetchFeed(int subscriptionCount) => subscriptionCount > 0;

    public async Task Execute(IJobExecutionContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        List<XkcdSubscription> subscriptions = await db.XkcdSubscriptions
            .Include(s => s.Webhook)
            .ToListAsync(cancellationToken);

        // Leave the feed unseeded until there is somewhere to post the initial comic.
        if (!ShouldFetchFeed(subscriptions.Count))
            return;

        bool hasSeen = await db.XkcdSeen.AnyAsync(cancellationToken);

        List<XkcdItem> items;
        try
        {
            items = await FetchItemsAsync(HttpClient, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logsService.Log($"XkcdJob: failed to fetch/parse feed: {ex.Message}", LogSeverity.Warning);
            return;
        }

        if (items.Count == 0)
            return;

        // First run ever: seed everything as seen so we don't backfill the whole archive.
        if (!hasSeen)
        {
            // Post only the most recent comic to existing subscribers as a kick-off. If an
            // earlier attempt is pending, keep retrying that same comic even if it has rotated
            // out of the RSS feed.
            string latestLink = await db.XkcdDeliveryRetries
                .OrderBy(r => r.LastAttemptAt)
                .Select(r => r.Link)
                .FirstOrDefaultAsync(cancellationToken) ?? items[0].Link;
            bool delivered = await DispatchAsync(
                latestLink,
                subscriptions,
                (subscription, link) => SendAsync(subscription, link, cancellationToken));
            if (!delivered)
            {
                int attempts = await RecordFailedDeliveryAsync(db, latestLink, DateTime.UtcNow, cancellationToken);
                if (ShouldRetryDelivery(attempts))
                    return;

                logsService.Log(
                    $"XkcdJob: giving up on {latestLink} after {attempts} hourly delivery attempts",
                    LogSeverity.Warning);
            }

            await ClearDeliveryRetryAsync(latestLink, cancellationToken);

            foreach (XkcdItem item in items)
                db.XkcdSeen.Add(new XkcdSeen { Link = item.Link, SeenAt = DateTime.UtcNow });
            if (items.All(item => item.Link != latestLink))
                db.XkcdSeen.Add(new XkcdSeen { Link = latestLink, SeenAt = DateTime.UtcNow });

            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        List<string> feedLinks = items.Select(i => i.Link).ToList();
        HashSet<string> seen = (await db.XkcdSeen
                .Where(x => feedLinks.Contains(x.Link))
                .Select(x => x.Link)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        // Retry pending comics first, including links that have rotated out of the RSS feed.
        // RSS is newest-first; post other new comics oldest-first so they read chronologically.
        List<string> pendingLinks = await db.XkcdDeliveryRetries
            .OrderBy(r => r.LastAttemptAt)
            .Select(r => r.Link)
            .ToListAsync(cancellationToken);
        HashSet<string> pendingLinkSet = pendingLinks.ToHashSet();
        List<string> deliveryLinks =
        [
            .. pendingLinks,
            .. items
                .Where(i => !seen.Contains(i.Link) && !pendingLinkSet.Contains(i.Link))
                .Reverse()
                .Select(i => i.Link)
        ];
        if (deliveryLinks.Count == 0)
            return;

        foreach (string link in deliveryLinks)
        {
            bool delivered = await DispatchAsync(
                link,
                subscriptions,
                (subscription, entryLink) => SendAsync(subscription, entryLink, cancellationToken));
            if (!delivered)
            {
                int attempts = await RecordFailedDeliveryAsync(db, link, DateTime.UtcNow, cancellationToken);
                if (ShouldRetryDelivery(attempts))
                    continue;

                logsService.Log(
                    $"XkcdJob: giving up on {link} after {attempts} hourly delivery attempts",
                    LogSeverity.Warning);
            }

            await ClearDeliveryRetryAsync(link, cancellationToken);
            db.XkcdSeen.Add(new XkcdSeen { Link = link, SeenAt = DateTime.UtcNow });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    internal static async Task<List<XkcdItem>> FetchItemsAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        string rss = await httpClient.GetStringAsync("https://xkcd.com/rss.xml", cancellationToken);
        XDocument doc = XDocument.Parse(rss);
        return doc.Descendants("item")
            .Select(x => new XkcdItem(
                x.Element("title")?.Value ?? string.Empty,
                x.Element("link")?.Value.Trim() ?? string.Empty))
            .Where(i => !string.IsNullOrEmpty(i.Link))
            .ToList();
    }

    internal static async Task<bool> DispatchAsync(
        string link,
        IReadOnlyList<XkcdSubscription> subscriptions,
        Func<XkcdSubscription, string, Task<bool>> sendAsync)
    {
        bool allSucceeded = true;
        foreach (XkcdSubscription sub in subscriptions)
        {
            if (!await sendAsync(sub, link))
                allSucceeded = false;
        }

        return allSucceeded;
    }

    internal static bool ShouldRetryDelivery(int attemptCount) => attemptCount < MaxDeliveryAttempts;

    internal static async Task<int> RecordFailedDeliveryAsync(
        DB db,
        string link,
        DateTime attemptedAt,
        CancellationToken cancellationToken = default)
    {
        XkcdDeliveryRetry? retry = await db.XkcdDeliveryRetries
            .SingleOrDefaultAsync(r => r.Link == link, cancellationToken);
        if (retry == null)
        {
            retry = new XkcdDeliveryRetry
            {
                Link = link,
                AttemptCount = 1,
                LastAttemptAt = attemptedAt
            };
            db.XkcdDeliveryRetries.Add(retry);
        }
        else
        {
            retry.AttemptCount++;
            retry.LastAttemptAt = attemptedAt;
        }

        await db.SaveChangesAsync(cancellationToken);
        return retry.AttemptCount;
    }

    private async Task ClearDeliveryRetryAsync(string link, CancellationToken cancellationToken)
    {
        XkcdDeliveryRetry? retry = await db.XkcdDeliveryRetries
            .SingleOrDefaultAsync(r => r.Link == link, cancellationToken);
        if (retry != null)
            db.XkcdDeliveryRetries.Remove(retry);
    }

    private async Task<bool> SendAsync(XkcdSubscription sub, string link, CancellationToken cancellationToken)
    {
        if (sub.Webhook == null)
        {
            logsService.Log($"XkcdJob: no webhook available in channel {sub.ChannelDiscordId}", LogSeverity.Warning);
            return false;
        }

        bool ok = await discordWebhook.SendAsync(
            sub.Webhook.WebhookId,
            sub.Webhook.Token,
            link,
            XkcdUsername,
            XkcdAvatarUrl,
            cancellationToken);
        if (!ok)
            logsService.Log($"XkcdJob: failed to post comic to channel {sub.ChannelDiscordId}", LogSeverity.Warning);

        return ok;
    }
}
