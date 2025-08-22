using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

public class QuoteApproval
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int QuoteApprovalMessageId { get; set; }

    [Required]
    public ulong UserId { get; set; }

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("QuoteApprovalMessageId")]
    public QuoteApprovalMessage? ApprovalMessage { get; set; }
}
