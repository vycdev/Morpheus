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
    public int MessageLength { get; set; } = 0;
    public double GuildAverageMessageLength { get; set; } = 0.0;
    public int GuildMessageCount { get; set; } = 0;
    public int XpGained { get; set; } = 0;
    
    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    // Foreign keys
    [ForeignKey("UserId")]
    public User User { get; set; }
    
    [ForeignKey("GuildId")]
    public Guild Guild { get; set; }
}
