using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

/// <summary>
/// Records an RSS/Atom entry that has already been dispatched to subscribers so it is never
/// posted twice. Keyed per feed URL (the same entry is the same regardless of which Discord
/// channel subscribes), so channels sharing a feed share seen state.
/// </summary>
public class RssSeenEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string FeedUrl { get; set; } = string.Empty;

    /// <summary>The entry's stable id (Atom id / RSS guid, falling back to its link).</summary>
    [Required]
    public string EntryId { get; set; } = string.Empty;

    public DateTime SeenAt { get; set; } = DateTime.UtcNow;
}
