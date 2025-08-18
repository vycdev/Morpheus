using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

public enum QuoteApprovalType
{
    AddRequest = 0,
    RemoveRequest = 1
}

public class QuoteApprovals
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int QuoteId { get; set; }

    [Required]
    public ulong ApprovalMessageId { get; set; }

    // A numeric score (e.g. votes/weighting) for this approval entry
    public int Score { get; set; } = 0;

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    [Required]
    public QuoteApprovalType Type { get; set; }

    // Navigation / foreign key
    [ForeignKey("QuoteId")]
    public Quote Quote { get; set; }
}
