using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

public class TemporaryBan
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    // Discord identifiers
    [Required]
    public ulong GuildId { get; set; }

    [Required]
    public ulong UserId { get; set; }

    // Optional context/reason
    public string? Reason { get; set; }

    // Timestamps
    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    // When the temporary ban expires and the user should be unbanned
    [Required]
    public DateTime ExpiresAt { get; set; }

    // Set when the unban actually happens (null means pending)
    public DateTime? UnbannedAt { get; set; }
}


