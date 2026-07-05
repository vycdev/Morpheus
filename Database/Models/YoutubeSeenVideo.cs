using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

/// <summary>
/// Records a YouTube video that has already been dispatched to subscribers so it is never
/// posted twice. A video is the same regardless of which Discord channel subscribes, so this
/// set is keyed by the video id globally.
/// </summary>
public class YoutubeSeenVideo
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>The YouTube channel the video belongs to (kept for bookkeeping / cleanup).</summary>
    [Required]
    public string YoutubeChannelId { get; set; } = string.Empty;

    /// <summary>The YouTube video id, used as the unique key.</summary>
    [Required]
    public string VideoId { get; set; } = string.Empty;

    public DateTime SeenAt { get; set; } = DateTime.UtcNow;
}
