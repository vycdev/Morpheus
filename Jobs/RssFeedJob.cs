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
    private const int DiscordMessageMaxLength = 2000;

    public async Task Execute(IJobExecutionContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        List<RssSubscription> subscriptions = await db.RssSubscriptions
            .Include(s => s.Webhook)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
            return;

        bool changed = false;

        foreach (IGrouping<string, RssSubscription> group in subscriptions.GroupBy(s => s.FeedUrl))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string feedUrl = group.Key;
            List<RssSubscription> subs = group.ToList();

            (string? _, string? _, IReadOnlyList<RssFeedService.FeedEntry> entries) = await rssFeed.FetchAsync(feedUrl, cancellationToken);
            if (entries.Count == 0)
                continue;

            (HashSet<string> seen, bool migrated) = await LoadSeenEntriesAsync(db, feedUrl, entries, cancellationToken);
            changed |= migrated;

            // If nothing from this feed has ever been seen, this is an initial run: mark
            // everything seen and only post the latest entry to avoid backfilling history.
            // Check all history for the feed because older seen entries may have rolled out of
            // the feed's current response.
            bool initialSeed = !await HasFeedHistoryAsync(db, feedUrl, cancellationToken);
            if (initialSeed)
            {
                RssFeedService.FeedEntry latest = entries.OrderByDescending(e => e.Published).First();
                if (!await DispatchAsync(latest, subs, (sub, content) => SendAsync(sub, content, cancellationToken), cancellationToken))
                    continue;

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
                cancellationToken.ThrowIfCancellationRequested();
                if (seen.Contains(entry.EntryId))
                    continue;

                if (!await DispatchAsync(entry, subs, (sub, content) => SendAsync(sub, content, cancellationToken), cancellationToken))
                    continue;

                db.RssSeenEntries.Add(new RssSeenEntry { FeedUrl = feedUrl, EntryId = entry.EntryId, SeenAt = DateTime.UtcNow });
                seen.Add(entry.EntryId);
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync(cancellationToken);
    }

    internal static async Task<(HashSet<string> Seen, bool Migrated)> LoadSeenEntriesAsync(
        DB db,
        string feedUrl,
        IReadOnlyList<RssFeedService.FeedEntry> entries,
        CancellationToken cancellationToken = default)
    {
        HashSet<string> currentIds = entries.Select(entry => entry.EntryId).ToHashSet();
        List<string> lookupIds = currentIds
            .Concat(entries.Select(entry => entry.LegacyEntryId).OfType<string>())
            .Distinct()
            .ToList();
        Dictionary<string, RssSeenEntry> history = await db.RssSeenEntries
            .Where(entry => entry.FeedUrl == feedUrl && lookupIds.Contains(entry.EntryId))
            .ToDictionaryAsync(entry => entry.EntryId, cancellationToken);
        HashSet<string> seen = history.Keys.ToHashSet();
        bool migrated = false;

        foreach (IGrouping<string, RssFeedService.FeedEntry> group in entries
                     .Where(entry => entry.LegacyEntryId is not null)
                     .GroupBy(entry => entry.LegacyEntryId!))
        {
            string legacyId = group.Key;
            if (currentIds.Contains(legacyId) || !history.TryGetValue(legacyId, out RssSeenEntry? legacy))
                continue;

            // A reused URL does not identify which entry was seen. An entry published
            // after the saved observation cannot own it; an undated entry could, so it
            // must remain a candidate when checking whether ownership is ambiguous.
            List<IGrouping<string, RssFeedService.FeedEntry>> candidates = group
                .Where(entry => entry.Published == DateTime.MinValue || entry.Published <= legacy.SeenAt)
                .GroupBy(entry => entry.EntryId)
                .ToList();
            if (candidates.Count != 1 || candidates[0].All(entry => entry.Published == DateTime.MinValue))
                continue;

            string entryId = candidates[0].Key;
            if (seen.Contains(entryId))
                continue;

            // Consume the old identity rather than retaining a permanent URL alias:
            // a later, distinct entry may legitimately reuse that URL.
            legacy.EntryId = entryId;
            seen.Remove(legacyId);
            seen.Add(entryId);
            migrated = true;
        }

        return (seen, migrated);
    }

    internal static async Task<bool> DispatchAsync(
        RssFeedService.FeedEntry entry,
        IReadOnlyList<RssSubscription> subs,
        Func<RssSubscription, string, Task<bool>> sendAsync,
        CancellationToken cancellationToken = default)
    {
        string content = FormatEntryContent(entry);
        if (string.IsNullOrWhiteSpace(content))
            return true;

        bool allSucceeded = true;
        foreach (RssSubscription sub in subs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await sendAsync(sub, content))
                allSucceeded = false;
        }

        return allSucceeded;
    }

    internal static string FormatEntryContent(RssFeedService.FeedEntry entry)
    {
        string content = !string.IsNullOrWhiteSpace(entry.Link) && entry.Link.Length <= DiscordMessageMaxLength
            ? entry.Link
            : !string.IsNullOrWhiteSpace(entry.Title)
                ? entry.Title
                : entry.Link;

        if (content.Length <= DiscordMessageMaxLength)
            return content;

        int prefixLength = DiscordMessageMaxLength - 1;
        if (char.IsHighSurrogate(content[prefixLength - 1]) && char.IsLowSurrogate(content[prefixLength]))
            prefixLength--;

        return string.Concat(content.AsSpan(0, prefixLength), "…");
    }

    internal static Task<bool> HasFeedHistoryAsync(DB db, string feedUrl, CancellationToken cancellationToken = default) =>
        db.RssSeenEntries.AnyAsync(entry => entry.FeedUrl == feedUrl, cancellationToken);

    private async Task<bool> SendAsync(RssSubscription sub, string content, CancellationToken cancellationToken)
    {
        if (sub.Webhook == null)
        {
            logsService.Log($"RssFeedJob: no webhook available for {sub.FeedUrl} in channel {sub.ChannelDiscordId}", LogSeverity.Warning);
            return false;
        }

        bool ok = await discordWebhook.SendAsync(sub.Webhook.WebhookId, sub.Webhook.Token, content, sub.DisplayName, sub.AvatarUrl, cancellationToken);
        if (!ok)
            logsService.Log($"RssFeedJob: failed to post entry from {sub.FeedUrl} to channel {sub.ChannelDiscordId}", LogSeverity.Warning);

        return ok;
    }
}
