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
        CancellationToken cancellationToken = context.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        if (!twitch.IsConfigured)
            return;

        List<TwitchSubscription> subscriptions = await db.TwitchSubscriptions
            .Include(s => s.Webhook)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
            return;

        List<string> userIds = subscriptions.Select(s => s.TwitchUserId).Distinct().ToList();
        TwitchService.LiveStreamsResult result = await twitch.GetLiveStreamsResultAsync(userIds, cancellationToken);
        if (!result.Succeeded)
        {
            logsService.Log("TwitchLiveJob: live-status request failed; preserving existing subscription state.", LogSeverity.Warning);
            return;
        }

        IReadOnlyDictionary<string, TwitchService.TwitchStream> live = result.Streams;

        bool changed = false;

        foreach (TwitchSubscription sub in subscriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            live.TryGetValue(sub.TwitchUserId, out TwitchService.TwitchStream? stream);
            changed |= await UpdateSubscriptionAsync(
                sub,
                stream,
                (subscription, liveStream) => AnnounceAsync(subscription, liveStream, cancellationToken),
                cancellationToken);
        }

        if (changed)
            await db.SaveChangesAsync(cancellationToken);
    }

    internal static async Task<bool> UpdateSubscriptionAsync(
        TwitchSubscription sub,
        TwitchService.TwitchStream? stream,
        Func<TwitchSubscription, TwitchService.TwitchStream, Task<bool>> announceAsync,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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

    private async Task<bool> AnnounceAsync(
        TwitchSubscription sub,
        TwitchService.TwitchStream stream,
        CancellationToken cancellationToken)
    {
        if (sub.Webhook == null)
            return false;

        string title = string.IsNullOrWhiteSpace(stream.Title) ? string.Empty : $"\n{stream.Title}";
        string content = $"🔴 **{sub.TwitchDisplayName}** is now live!{title}\nhttps://www.twitch.tv/{sub.TwitchLogin}";

        bool ok = await discordWebhook.SendAsync(
            sub.Webhook.WebhookId,
            sub.Webhook.Token,
            content,
            sub.TwitchDisplayName,
            sub.AvatarUrl,
            cancellationToken);
        if (!ok)
            logsService.Log($"TwitchLiveJob: failed to post go-live for {sub.TwitchLogin} to channel {sub.ChannelDiscordId}", LogSeverity.Warning);

        return ok;
    }
}
