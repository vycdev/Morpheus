using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

/// <summary>
/// Marks a Discord channel as an xkcd feed channel: every time a new xkcd comic is published
/// it is posted here through the channel's shared <see cref="Webhook"/>.
/// </summary>
public class XkcdSubscription
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public ulong GuildDiscordId { get; set; }

    /// <summary>The Discord channel new comics are posted to. One xkcd subscription per channel.</summary>
    [Required]
    public ulong ChannelDiscordId { get; set; }

    /// <summary>The reusable webhook used to post to the channel.</summary>
    [Required]
    public int WebhookId { get; set; }

    [ForeignKey(nameof(WebhookId))]
    public Webhook? Webhook { get; set; }

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;
}
