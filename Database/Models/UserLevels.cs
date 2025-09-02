using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

public class UserLevels
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int GuildId { get; set; }

    public int Level { get; set; } = 0;
    public int TotalXp { get; set; } = 0;

    // Per-user message stats (per guild)
    // Total number of messages observed for this user in this guild
    public int UserMessageCount { get; set; } = 0;
    // Regular running average of raw message length (characters)
    public double UserAverageMessageLength { get; set; } = 0.0;
    // Exponential moving average of raw message length (characters)
    public double UserAverageMessageLengthEma { get; set; } = 0.0;

    // Foreign keys
    [ForeignKey("UserId")]
    public User? User { get; set; }

    [ForeignKey("GuildId")]
    public Guild? Guild { get; set; }

}
