using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

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
    public long TotalXp { get; set; } = 0;
    public int Level { get; set; } = 0;
    public bool LevelUpMessages { get; set; } = true;
    public bool LevelUpQuotes { get; set; } = true;

    // Foreign keys
    public List<Quote> Quotes { get; set; }
    public List<ButtonGamePress> ButtonGamePresses { get; set; }
    public List<UserActivity> UserActivities { get; set; }
}
