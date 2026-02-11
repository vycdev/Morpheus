using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Morpheus.Database.Enums;

namespace Morpheus.Database.Models;

public class StockTransaction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public int? StockId { get; set; }

    public int? TargetUserId { get; set; }

    [Required]
    public TransactionType Type { get; set; }

    /// <summary>
    /// The money amount involved in the transaction.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Fee charged for this transaction.
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    /// Number of shares involved (buy/sell only).
    /// </summary>
    public decimal? Shares { get; set; }

    /// <summary>
    /// Stock price at the time of transaction (buy/sell only).
    /// </summary>
    public decimal? PriceAtTransaction { get; set; }

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    // Foreign keys
    [ForeignKey("UserId")]
    public User? User { get; set; }

    [ForeignKey("StockId")]
    public Stock? Stock { get; set; }

    [ForeignKey("TargetUserId")]
    public User? TargetUser { get; set; }
}
