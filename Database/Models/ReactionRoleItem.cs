using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

public class ReactionRoleItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int ReactionRoleMessageId { get; set; }

    [Required]
    public ulong RoleId { get; set; }

    [Required]
    public string Emoji { get; set; } = "";

    [Required]
    public string CustomId { get; set; } = "";

    [ForeignKey("ReactionRoleMessageId")]
    public ReactionRoleMessage ReactionRoleMessage { get; set; } = null!;
}
