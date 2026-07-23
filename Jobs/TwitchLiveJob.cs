using Discord;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

/// <summary>
/// Polls Twitch for subscribed streamers and posts a "went live" notification (through the
/// channel's shared webhook, as the streamer) when one starts streaming. Each stream is only
/// announced once by tracking the last-announced stream id per subscription.
/// </summary>
[DisallowConcurrentExecution]
public class TwitchLiveJob(DB db, TwitchService twitch, DiscordWebhookService discordWebhook, LogsService logsService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!twitch.IsConfigured)
            return;

        List<TwitchSubscription> subscriptions = await db.TwitchSubscriptions
            .Include(s => s.Webhook)
            .ToListAsync();

        if (subscriptions.Count == 0)
            return;

        List<string> userIds = subscriptions.Select(s => s.TwitchUserId).Distinct().ToList();
        IReadOnlyDictionary<string, TwitchService.TwitchStream> live = await twitch.GetLiveStreamsAsync(userIds);

        bool changed = false;

        foreach (TwitchSubscription sub in subscriptions)
        {
            live.TryGetValue(sub.TwitchUserId, out TwitchService.TwitchStream? stream);
            changed |= await UpdateSubscriptionAsync(sub, stream, AnnounceAsync);
        }

        if (changed)
            await db.SaveChangesAsync();
    }

    internal static async Task<bool> UpdateSubscriptionAsync(
        TwitchSubscription sub,
        TwitchService.TwitchStream? stream,
        Func<TwitchSubscription, TwitchService.TwitchStream, Task<bool>> announceAsync)
    {
        bool changed = false;

        if (stream != null)
        {
            // Record a stream only after Discord accepts the notification. A failed delivery is
            // retried on the next poll instead of being silently treated as announced.
            if (sub.LastAnnouncedStreamId != stream.Id && await announceAsync(sub, stream))
            {
                sub.LastAnnouncedStreamId = stream.Id;
                changed = true;
            }

            if (!sub.IsLive)
            {
                sub.IsLive = true;
                changed = true;
            }
        }
        else if (sub.IsLive)
        {
            sub.IsLive = false;
            changed = true;
        }

        return changed;
    }

    private async Task<bool> AnnounceAsync(TwitchSubscription sub, TwitchService.TwitchStream stream)
    {
        if (sub.Webhook == null)
            return false;

        string title = string.IsNullOrWhiteSpace(stream.Title) ? string.Empty : $"\n{stream.Title}";
        string content = $"🔴 **{sub.TwitchDisplayName}** is now live!{title}\nhttps://www.twitch.tv/{sub.TwitchLogin}";

        bool ok = await discordWebhook.SendAsync(sub.Webhook.WebhookId, sub.Webhook.Token, content, sub.TwitchDisplayName, sub.AvatarUrl);
        if (!ok)
            logsService.Log($"TwitchLiveJob: failed to post go-live for {sub.TwitchLogin} to channel {sub.ChannelDiscordId}", LogSeverity.Warning);

        return ok;
    }
}
