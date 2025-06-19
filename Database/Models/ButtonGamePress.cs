using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

public class ButtonGamePress
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }
    public int? GuildId { get; set; }

    public long Score { get; set; } = 0;
    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    // Foreign keys
    public User User { get; set; }
    public Guild Guild { get; set; }
}
