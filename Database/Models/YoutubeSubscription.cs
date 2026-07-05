using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

/// <summary>
/// A subscription that posts a YouTuber's new uploads into a Discord channel through the
/// channel's shared <see cref="Webhook"/>. The webhook posts as the YouTuber (username +
/// avatar overridden per message).
/// </summary>
public class YoutubeSubscription
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public ulong GuildDiscordId { get; set; }

    /// <summary>The Discord channel new videos are posted to.</summary>
    [Required]
    public ulong ChannelDiscordId { get; set; }

    /// <summary>The YouTube channel id (e.g. "UCxxxxxxxxxxxxxxxxxxxxxx").</summary>
    [Required]
    public string YoutubeChannelId { get; set; } = string.Empty;

    /// <summary>Cached YouTube channel title, used as the webhook username.</summary>
    public string YoutubeChannelTitle { get; set; } = string.Empty;

    /// <summary>Cached YouTube channel avatar URL, used as the webhook avatar.</summary>
    public string? YoutubeAvatarUrl { get; set; }

    /// <summary>The reusable webhook used to post to the channel.</summary>
    [Required]
    public int WebhookId { get; set; }

    [ForeignKey(nameof(WebhookId))]
    public Webhook? Webhook { get; set; }

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;
}
