using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

/// <summary>
/// Tracks consecutive hourly delivery attempts for an xkcd comic so a broken webhook cannot
/// cause successful subscribers to receive the same comic indefinitely.
/// </summary>
public class XkcdDeliveryRetry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string Link { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    public DateTime LastAttemptAt { get; set; } = DateTime.UtcNow;
}
