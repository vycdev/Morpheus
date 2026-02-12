using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;

public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public ulong DiscordId { get; set; }

    public string Username { get; set; }

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    public DateTime LastUsernameCheck { get; set; } = DateTime.UtcNow;

    // Leveling system 
    public bool LevelUpMessages { get; set; } = true;
    public bool LevelUpQuotes { get; set; } = true;

    // Economy
    public decimal Balance { get; set; } = 1000.00m;

    public DateTime LastRobberyAttempt { get; set; } = DateTime.MinValue;
    public DateTime LastSuccessfullyRobbed { get; set; } = DateTime.MinValue;

    // Foreign keys
    public List<Quote> Quotes { get; set; }
    public List<ButtonGamePress> ButtonGamePresses { get; set; }
    public List<UserActivity> UserActivity { get; set; }
    public List<UserLevels> UserLevels { get; set; }
    public List<StockHolding> StockHoldings { get; set; }
    public List<StockTransaction> StockTransactions { get; set; }
}
