using Discord;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

/// <summary>
/// Periodically verifies that stored webhooks still exist on Discord. If a webhook was deleted
/// (e.g. a moderator removed it), its row is deleted, which cascades to the subscriptions that
/// used it — effectively turning those feeds off until re-configured.
/// </summary>
[DisallowConcurrentExecution]
public class WebhookHealthJob(DB db, DiscordWebhookService discordWebhook, LogsService logsService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        List<Webhook> webhooks = await db.Webhooks.ToListAsync();
        if (webhooks.Count == 0)
            return;

        int removed = 0;
        bool changed = false;

        foreach (Webhook webhook in webhooks)
        {
            bool? exists = await discordWebhook.CheckExistsAsync(webhook.WebhookId, webhook.Token);

            if (exists == false)
            {
                // Definitively gone — remove it. Cascade deletes any subscriptions that used it.
                db.Webhooks.Remove(webhook);
                removed++;
                changed = true;
            }
            else if (exists == true)
            {
                webhook.LastCheckedAt = DateTime.UtcNow;
                changed = true;
            }
            // null => indeterminate (transient error); leave untouched and retry next run.
        }

        if (changed)
            await db.SaveChangesAsync();

        if (removed > 0)
            logsService.Log($"WebhookHealthJob: removed {removed} deleted webhook(s) and their subscriptions.");
    }
}
