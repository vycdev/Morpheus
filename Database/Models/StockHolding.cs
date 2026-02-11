using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

public class StockHolding
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int StockId { get; set; }

    /// <summary>
    /// Number of shares owned.
    /// </summary>
    public decimal Shares { get; set; } = 0m;

    /// <summary>
    /// Cumulative amount invested after fees, for P&L calculation.
    /// </summary>
    public decimal TotalInvested { get; set; } = 0m;

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    // Foreign keys
    [ForeignKey("UserId")]
    public User? User { get; set; }

    [ForeignKey("StockId")]
    public Stock? Stock { get; set; }
}
