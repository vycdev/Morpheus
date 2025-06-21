using Morpheus.Database.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;
public class Role
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int GuildId { get; set; }

    [Required]
    public ulong RoleId { get; set; }

    [Required]
    public RoleType RoleType { get; set; }

    // Foreign key to Guild
    [ForeignKey("GuildId")]
    public Guild Guild { get; set; } = null!;
}
