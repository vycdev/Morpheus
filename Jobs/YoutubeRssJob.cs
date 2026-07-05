using Discord;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Morpheus.Utilities;
using Quartz;

namespace Morpheus.Jobs;

/// <summary>
/// Checks every subscribed YouTuber's uploads feed and posts new videos to the subscribing
/// channels through their shared webhooks, posting as the YouTuber (username + avatar). Seen
/// videos are recorded globally so they never repeat across channels.
/// </summary>
[DisallowConcurrentExecution]
public class YoutubeRssJob(DB db, YoutubeFeedService youtubeFeed, DiscordWebhookService discordWebhook, LogsService logsService) : IJob
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task Execute(IJobExecutionContext context)
    {
        List<YoutubeSubscription> subscriptions = await db.YoutubeSubscriptions
            .Include(s => s.Webhook)
            .ToListAsync();

        if (subscriptions.Count == 0)
            return;

        bool changed = false;

        foreach (IGrouping<string, YoutubeSubscription> group in subscriptions.GroupBy(s => s.YoutubeChannelId))
        {
            string youtubeChannelId = group.Key;
            List<YoutubeSubscription> subs = group.ToList();

            (string? channelTitle, IReadOnlyList<YoutubeFeedService.VideoEntry> entries) = await youtubeFeed.FetchFeedAsync(youtubeChannelId);
            if (entries.Count == 0)
                continue;

            // Refresh cached identity (title / avatar) used as the webhook username + avatar.
            string username = !string.IsNullOrWhiteSpace(channelTitle) ? channelTitle! : subs[0].YoutubeChannelTitle;
            string? avatar = subs.Select(s => s.YoutubeAvatarUrl).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));
            if (string.IsNullOrWhiteSpace(avatar))
                avatar = await YoutubeUtils.GetChannelAvatarAsync(HttpClient, youtubeChannelId);

            foreach (YoutubeSubscription sub in subs)
            {
                if (!string.IsNullOrWhiteSpace(channelTitle) && sub.YoutubeChannelTitle != channelTitle)
                {
                    sub.YoutubeChannelTitle = channelTitle!;
                    changed = true;
                }
                if (!string.IsNullOrWhiteSpace(avatar) && sub.YoutubeAvatarUrl != avatar)
                {
                    sub.YoutubeAvatarUrl = avatar;
                    changed = true;
                }
            }

            List<string> videoIds = entries.Select(e => e.VideoId).ToList();
            HashSet<string> seen = (await db.YoutubeSeenVideos
                    .Where(v => videoIds.Contains(v.VideoId))
                    .Select(v => v.VideoId)
                    .ToListAsync())
                .ToHashSet();

            // If nothing from this channel has ever been seen, this is an initial run for it:
            // mark everything seen and only post the latest video to avoid backfilling history.
            bool initialSeed = !entries.Any(e => seen.Contains(e.VideoId));
            if (initialSeed)
            {
                YoutubeFeedService.VideoEntry latest = entries.OrderByDescending(e => e.Published).First();
                await DispatchAsync(latest, subs, username, avatar);

                foreach (YoutubeFeedService.VideoEntry entry in entries)
                {
                    if (seen.Add(entry.VideoId))
                        db.YoutubeSeenVideos.Add(new YoutubeSeenVideo { YoutubeChannelId = youtubeChannelId, VideoId = entry.VideoId, SeenAt = DateTime.UtcNow });
                }
                changed = true;
                continue;
            }

            foreach (YoutubeFeedService.VideoEntry entry in entries.OrderBy(e => e.Published))
            {
                if (seen.Contains(entry.VideoId))
                    continue;

                await DispatchAsync(entry, subs, username, avatar);

                db.YoutubeSeenVideos.Add(new YoutubeSeenVideo { YoutubeChannelId = youtubeChannelId, VideoId = entry.VideoId, SeenAt = DateTime.UtcNow });
                seen.Add(entry.VideoId);
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }

    private async Task DispatchAsync(YoutubeFeedService.VideoEntry entry, List<YoutubeSubscription> subs, string username, string? avatar)
    {
        foreach (YoutubeSubscription sub in subs)
        {
            if (sub.Webhook == null)
                continue;

            bool ok = await discordWebhook.SendAsync(sub.Webhook.WebhookId, sub.Webhook.Token, entry.Link, username, avatar);
            if (!ok)
                logsService.Log($"YoutubeRssJob: failed to post {entry.VideoId} to channel {sub.ChannelDiscordId}", LogSeverity.Warning);
        }
    }
}
