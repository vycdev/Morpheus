using Discord;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

/// <summary>
/// Checks every subscribed RSS/Atom feed and posts new entries to the subscribing channels
/// through their shared webhooks, posting under each subscription's display name + avatar. Seen
/// entries are recorded per feed so they never repeat across channels.
/// </summary>
[DisallowConcurrentExecution]
public class RssFeedJob(DB db, RssFeedService rssFeed, DiscordWebhookService discordWebhook, LogsService logsService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        List<RssSubscription> subscriptions = await db.RssSubscriptions
            .Include(s => s.Webhook)
            .ToListAsync();

        if (subscriptions.Count == 0)
            return;

        bool changed = false;

        foreach (IGrouping<string, RssSubscription> group in subscriptions.GroupBy(s => s.FeedUrl))
        {
            string feedUrl = group.Key;
            List<RssSubscription> subs = group.ToList();

            (string? _, string? _, IReadOnlyList<RssFeedService.FeedEntry> entries) = await rssFeed.FetchAsync(feedUrl);
            if (entries.Count == 0)
                continue;

            List<string> entryIds = entries.Select(e => e.EntryId).ToList();
            HashSet<string> seen = (await db.RssSeenEntries
                    .Where(v => v.FeedUrl == feedUrl && entryIds.Contains(v.EntryId))
                    .Select(v => v.EntryId)
                    .ToListAsync())
                .ToHashSet();

            // If nothing from this feed has ever been seen, this is an initial run: mark
            // everything seen and only post the latest entry to avoid backfilling history.
            bool initialSeed = !entries.Any(e => seen.Contains(e.EntryId));
            if (initialSeed)
            {
                RssFeedService.FeedEntry latest = entries.OrderByDescending(e => e.Published).First();
                await DispatchAsync(latest, subs);

                foreach (RssFeedService.FeedEntry entry in entries)
                {
                    if (seen.Add(entry.EntryId))
                        db.RssSeenEntries.Add(new RssSeenEntry { FeedUrl = feedUrl, EntryId = entry.EntryId, SeenAt = DateTime.UtcNow });
                }
                changed = true;
                continue;
            }

            foreach (RssFeedService.FeedEntry entry in entries.OrderBy(e => e.Published))
            {
                if (seen.Contains(entry.EntryId))
                    continue;

                await DispatchAsync(entry, subs);

                db.RssSeenEntries.Add(new RssSeenEntry { FeedUrl = feedUrl, EntryId = entry.EntryId, SeenAt = DateTime.UtcNow });
                seen.Add(entry.EntryId);
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }

    private async Task DispatchAsync(RssFeedService.FeedEntry entry, List<RssSubscription> subs)
    {
        string content = !string.IsNullOrWhiteSpace(entry.Link) ? entry.Link : entry.Title;
        if (string.IsNullOrWhiteSpace(content))
            return;

        foreach (RssSubscription sub in subs)
        {
            if (sub.Webhook == null)
                continue;

            bool ok = await discordWebhook.SendAsync(sub.Webhook.WebhookId, sub.Webhook.Token, content, sub.DisplayName, sub.AvatarUrl);
            if (!ok)
                logsService.Log($"RssFeedJob: failed to post entry from {sub.FeedUrl} to channel {sub.ChannelDiscordId}", LogSeverity.Warning);
        }
    }
}
