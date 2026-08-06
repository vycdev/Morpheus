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
            // Check all history for the channel because older seen videos may have rolled out of
            // the feed's current response.
            bool initialSeed = !await HasFeedHistoryAsync(db, youtubeChannelId);
            if (initialSeed)
            {
                YoutubeFeedService.VideoEntry latest = entries.OrderByDescending(e => e.Published).First();
                if (!await DispatchAsync(subs, sub => SendAsync(sub, latest, username, avatar)))
                    continue;

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

                if (!await DispatchAsync(subs, sub => SendAsync(sub, entry, username, avatar)))
                    continue;

                db.YoutubeSeenVideos.Add(new YoutubeSeenVideo { YoutubeChannelId = youtubeChannelId, VideoId = entry.VideoId, SeenAt = DateTime.UtcNow });
                seen.Add(entry.VideoId);
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }

    internal static async Task<bool> DispatchAsync(
        IReadOnlyList<YoutubeSubscription> subs,
        Func<YoutubeSubscription, Task<bool>> sendAsync)
    {
        bool allSucceeded = true;
        foreach (YoutubeSubscription sub in subs)
        {
            if (!await sendAsync(sub))
                allSucceeded = false;
        }

        return allSucceeded;
    }

    internal static Task<bool> HasFeedHistoryAsync(DB db, string youtubeChannelId) =>
        db.YoutubeSeenVideos.AnyAsync(video => video.YoutubeChannelId == youtubeChannelId);

    private async Task<bool> SendAsync(
        YoutubeSubscription sub,
        YoutubeFeedService.VideoEntry entry,
        string username,
        string? avatar)
    {
        if (sub.Webhook == null)
        {
            logsService.Log($"YoutubeRssJob: no webhook available for {sub.YoutubeChannelId} in channel {sub.ChannelDiscordId}", LogSeverity.Warning);
            return false;
        }

        bool ok = await discordWebhook.SendAsync(sub.Webhook.WebhookId, sub.Webhook.Token, entry.Link, username, avatar);
        if (!ok)
            logsService.Log($"YoutubeRssJob: failed to post {entry.VideoId} to channel {sub.ChannelDiscordId}", LogSeverity.Warning);

        return ok;
    }
}
