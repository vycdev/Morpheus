using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

public class UserActivity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int GuildId { get; set; }

    [Required]
    public ulong DiscordChannelId { get; set; }

    public string MessageHash { get; set; } = string.Empty;
    // 64-bit SimHash over normalized character trigrams of the message (non-reversible)
    public ulong MessageSimHash { get; set; } = 0UL;
    // Length of the normalized text used for SimHash (helps ignore very short messages)
    public int NormalizedLength { get; set; } = 0;
    public int MessageLength { get; set; } = 0;
    public double GuildAverageMessageLength { get; set; } = 0.0;
    public int GuildMessageCount { get; set; } = 0;
    public int XpGained { get; set; } = 0;

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    // Foreign keys
    [ForeignKey("UserId")]
    public User? User { get; set; }

    [ForeignKey("GuildId")]
    public Guild? Guild { get; set; }
}
