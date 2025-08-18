using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

public class Quote
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int GuildId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public string Content { get; set; }

    public bool Approved { get; set; } = false;

    public bool Removed { get; set; } = false;

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    // Foreign keys
    [ForeignKey("UserId")]
    public User User { get; set; }

    [ForeignKey("GuildId")]
    public Guild Guild { get; set; }

    public List<QuoteScore> Scores { get; set; }
}
