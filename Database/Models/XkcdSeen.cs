using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

/// <summary>
/// Records an xkcd comic that has already been dispatched to subscribers so it is never
/// posted twice. xkcd is the same for everyone, so this set is global (not per channel).
/// </summary>
public class XkcdSeen
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>The comic permalink, used as the unique key.</summary>
    [Required]
    public string Link { get; set; } = string.Empty;

    public DateTime SeenAt { get; set; } = DateTime.UtcNow;
}
