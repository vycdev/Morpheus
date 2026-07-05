using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

/// <summary>
/// A subscription that posts a "went live" notification into a Discord channel when a Twitch
/// streamer starts streaming, through the channel's shared <see cref="Webhook"/> (posting as the
/// streamer). Live state is tracked so each stream is only announced once.
/// </summary>
public class TwitchSubscription
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public ulong GuildDiscordId { get; set; }

    /// <summary>The Discord channel go-live notifications are posted to.</summary>
    [Required]
    public ulong ChannelDiscordId { get; set; }

    /// <summary>The Twitch numeric user id (stable across name changes).</summary>
    [Required]
    public string TwitchUserId { get; set; } = string.Empty;

    /// <summary>The Twitch login (lowercase handle used in the URL).</summary>
    [Required]
    public string TwitchLogin { get; set; } = string.Empty;

    /// <summary>Cached Twitch display name, used as the webhook username.</summary>
    public string TwitchDisplayName { get; set; } = string.Empty;

    /// <summary>Cached Twitch profile image URL, used as the webhook avatar.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>Whether the streamer was live at the last check.</summary>
    public bool IsLive { get; set; }

    /// <summary>The Twitch stream id last announced, so a single stream is only announced once.</summary>
    public string? LastAnnouncedStreamId { get; set; }

    /// <summary>The reusable webhook used to post to the channel.</summary>
    [Required]
    public int WebhookId { get; set; }

    [ForeignKey(nameof(WebhookId))]
    public Webhook? Webhook { get; set; }

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;
}
