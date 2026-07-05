using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

/// <summary>
/// A generic RSS/Atom feed subscription that posts new entries into a Discord channel through
/// the channel's shared <see cref="Webhook"/>. Works for blogs (e.g. the vycdev blog) and GitHub
/// Atom feeds (releases/commits/tags), and any other RSS 2.0 or Atom feed.
/// </summary>
public class RssSubscription
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public ulong GuildDiscordId { get; set; }

    /// <summary>The Discord channel new entries are posted to.</summary>
    [Required]
    public ulong ChannelDiscordId { get; set; }

    /// <summary>The feed URL (RSS 2.0 or Atom).</summary>
    [Required]
    public string FeedUrl { get; set; } = string.Empty;

    /// <summary>The webhook username used when posting this feed's entries.</summary>
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional webhook avatar URL used when posting this feed's entries.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>The reusable webhook used to post to the channel.</summary>
    [Required]
    public int WebhookId { get; set; }

    [ForeignKey(nameof(WebhookId))]
    public Webhook? Webhook { get; set; }

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;
}
