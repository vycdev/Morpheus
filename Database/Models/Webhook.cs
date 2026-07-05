using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

/// <summary>
/// A reusable Discord webhook. At most one webhook is stored per Discord channel so that
/// multiple features (xkcd, YouTube subscriptions, future RSS feeds, ...) can share it and
/// stay within Discord's per-channel webhook limit. The webhook identity (username + avatar)
/// is overridden per message, so a single webhook can post as "xkcd", a YouTuber, etc.
/// </summary>
public class Webhook
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public ulong GuildDiscordId { get; set; }

    /// <summary>The Discord channel this webhook posts to. Unique across the table.</summary>
    [Required]
    public ulong ChannelDiscordId { get; set; }

    /// <summary>The Discord webhook id.</summary>
    [Required]
    public ulong WebhookId { get; set; }

    /// <summary>The Discord webhook token (used together with WebhookId to execute the webhook).</summary>
    [Required]
    public string Token { get; set; } = string.Empty;

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    /// <summary>Last time the health job confirmed this webhook still exists on Discord.</summary>
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;
}
