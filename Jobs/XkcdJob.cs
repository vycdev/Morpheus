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
    // Public square avatar for the xkcd identity (data: URLs are rejected by Discord's avatar_url).
    private const string XkcdAvatarUrl = "https://pbs.twimg.com/profile_images/1488600831377252354/hEpPeSu0_400x400.jpg";

    private record XkcdItem(string Title, string Link);

    internal static bool ShouldFetchFeed(int subscriptionCount) => subscriptionCount > 0;

    public async Task Execute(IJobExecutionContext context)
    {
        List<XkcdSubscription> subscriptions = await db.XkcdSubscriptions
            .Include(s => s.Webhook)
            .ToListAsync();

        // Leave the feed unseeded until there is somewhere to post the initial comic.
        if (!ShouldFetchFeed(subscriptions.Count))
            return;

        bool hasSeen = await db.XkcdSeen.AnyAsync();

        List<XkcdItem> items;
        try
        {
            string rss = await HttpClient.GetStringAsync("https://xkcd.com/rss.xml");
            XDocument doc = XDocument.Parse(rss);
            items = doc.Descendants("item")
                .Select(x => new XkcdItem(
                    x.Element("title")?.Value ?? string.Empty,
                    x.Element("link")?.Value ?? string.Empty))
                .Where(i => !string.IsNullOrEmpty(i.Link))
                .ToList();
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
            foreach (XkcdItem item in items)
                db.XkcdSeen.Add(new XkcdSeen { Link = item.Link, SeenAt = DateTime.UtcNow });

            // Post only the most recent comic to existing subscribers as a kick-off.
            XkcdItem latest = items[0];
            await DispatchAsync(latest, subscriptions);
            await db.SaveChangesAsync();
            return;
        }

        List<string> feedLinks = items.Select(i => i.Link).ToList();
        HashSet<string> seen = (await db.XkcdSeen
                .Where(x => feedLinks.Contains(x.Link))
                .Select(x => x.Link)
                .ToListAsync())
            .ToHashSet();

        // RSS is newest-first; post new comics oldest-first so they read chronologically.
        List<XkcdItem> newItems = items.Where(i => !seen.Contains(i.Link)).Reverse().ToList();
        if (newItems.Count == 0)
            return;

        foreach (XkcdItem item in newItems)
        {
            await DispatchAsync(item, subscriptions);
            db.XkcdSeen.Add(new XkcdSeen { Link = item.Link, SeenAt = DateTime.UtcNow });
        }

        await db.SaveChangesAsync();
    }

    private async Task DispatchAsync(XkcdItem item, List<XkcdSubscription> subscriptions)
    {
        foreach (XkcdSubscription sub in subscriptions)
        {
            if (sub.Webhook == null)
                continue;

            await discordWebhook.SendAsync(sub.Webhook.WebhookId, sub.Webhook.Token, item.Link, XkcdUsername, XkcdAvatarUrl);
        }
    }
}
