using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

public class ReactionRoleMessage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int GuildId { get; set; }

    [Required]
    public ulong ChannelId { get; set; }

    [Required]
    public ulong MessageId { get; set; }

    public bool UseButtons { get; set; } = false;

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    [ForeignKey("GuildId")]
    public Guild Guild { get; set; } = null!;

    public List<ReactionRoleItem> Items { get; set; } = [];
}
