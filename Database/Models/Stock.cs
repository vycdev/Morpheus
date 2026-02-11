using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Morpheus.Database.Enums;

namespace Morpheus.Database.Models;

public class Stock
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public StockEntityType EntityType { get; set; }

    [Required]
    public int EntityId { get; set; }

    public decimal Price { get; set; } = 100.00m;

    public decimal PreviousPrice { get; set; } = 100.00m;

    public decimal DailyChangePercent { get; set; } = 0m;

    /// <summary>
    /// Randomly assigned fixed time of day (minutes since midnight UTC) for daily price update.
    /// </summary>
    public int UpdateTimeMinutes { get; set; } = 0;

    public DateTime LastUpdatedDate { get; set; } = DateTime.MinValue;

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<StockHolding> StockHoldings { get; set; }
}
