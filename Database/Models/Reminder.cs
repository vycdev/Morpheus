using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

public class Reminder
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public ulong ChannelId { get; set; }
    public ulong? PingUserId { get; set; }
    public string? Text { get; set; }

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    public DateTime DueDate { get; set; }

    public int? GuildId { get; set; }

    [ForeignKey(nameof(GuildId))]
    public Guild? Guild { get; set; }

    public int? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}
