using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;

namespace Morpheus.Services;

/// <summary>
/// Manages the single reusable <see cref="Webhook"/> that features share per Discord channel.
/// Creates it on Discord on demand (reusing an existing bot-owned webhook where possible to
/// stay under Discord's per-channel limit) and persists it so jobs can post without needing
/// the gateway channel object.
/// </summary>
public class WebhookService(DB db, DiscordSocketClient client, DiscordWebhookService discordWebhook, LogsService logsService)
{
    private const string WebhookName = "Morpheus";

    /// <summary>
    /// Returns the stored webhook for the channel, creating (or re-creating) it on Discord if
    /// needed. Returns null if the webhook could not be created (e.g. missing permissions).
    /// </summary>
    public async Task<Webhook?> GetOrCreateWebhookAsync(ITextChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        Webhook? stored = await db.Webhooks.FirstOrDefaultAsync(w => w.ChannelDiscordId == channel.Id);

        if (stored != null)
        {
            bool? exists = await discordWebhook.CheckExistsAsync(stored.WebhookId, stored.Token);
            if (exists != false)
            {
                // Exists, or state indeterminate — keep using it.
                stored.LastCheckedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return stored;
            }

            // Definitively gone on Discord — recreate and refresh the row in place so existing
            // subscriptions that reference this webhook keep working.
            IWebhook? recreatedHook = await TryCreateDiscordWebhookAsync(channel);
            if (recreatedHook == null)
                return null;

            stored.WebhookId = recreatedHook.Id;
            stored.Token = recreatedHook.Token;
            stored.LastCheckedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return stored;
        }

        // Nothing stored: reuse an existing bot-owned webhook if there is one, else create.
        IWebhook? hook = await TryReuseExistingWebhookAsync(channel) ?? await TryCreateDiscordWebhookAsync(channel);
        if (hook == null)
            return null;

        Webhook created = new()
        {
            GuildDiscordId = channel.GuildId,
            ChannelDiscordId = channel.Id,
            WebhookId = hook.Id,
            Token = hook.Token,
            InsertDate = DateTime.UtcNow,
            LastCheckedAt = DateTime.UtcNow
        };

        db.Webhooks.Add(created);
        await db.SaveChangesAsync();
        return created;
    }

    private async Task<IWebhook?> TryReuseExistingWebhookAsync(ITextChannel channel)
    {
        try
        {
            IReadOnlyCollection<IWebhook> hooks = await channel.GetWebhooksAsync();
            return hooks.FirstOrDefault(w =>
                !string.IsNullOrEmpty(w.Token) &&
                w.Creator?.Id == client.CurrentUser.Id);
        }
        catch (Exception ex)
        {
            logsService.Log($"Failed to list webhooks for channel {channel.Id}: {ex.Message}", LogSeverity.Warning);
            return null;
        }
    }

    private async Task<IWebhook?> TryCreateDiscordWebhookAsync(ITextChannel channel)
    {
        try
        {
            return await channel.CreateWebhookAsync(WebhookName);
        }
        catch (Exception ex)
        {
            logsService.Log($"Failed to create webhook in channel {channel.Id}: {ex.Message}", LogSeverity.Warning);
            return null;
        }
    }
}
