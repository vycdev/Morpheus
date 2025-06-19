using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;
public class QuoteScore
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int QuoteId { get; set; }

    [Required]
    public int UserId { get; set; }
    public int Score { get; set; } = 0;
    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    // Foreign keys
    [ForeignKey("QuoteId")]
    public Quote Quote { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; }
}
