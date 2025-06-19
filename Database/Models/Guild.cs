using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Morpheus.Database.Models;
public class Guild
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public ulong DiscordId { get; set; }
    public string Name { get; set; } = "";

    // Settings
    public string Prefix { get; set; } = "m!";

    // Channels 
    public ulong WelcomeChannelId { get; set; }
    public ulong PinsChannelId { get; set; }
    public ulong LevelUpMessagesChannelId { get; set; }
    public ulong LevelUpQuotesChannelId { get; set; }

    // Leveling and Quotes system settings 
    public bool LevelUpMessages { get; set; } = false;
    public bool LevelUpQuotes { get; set; } = false;
    public bool UseGlobalQuotes { get; set; } = false;
    public ulong QuotesApprovalChannelId { get; set; }
    public int QuoteAddRequiredApprovals { get; set; } = 5;
    public int QuoteRemoveRequiredApprovals { get; set; } = 5;

    // Settings 
    public bool WelcomeMessages { get; set; } = true;
    public bool UseActivityRoles { get; set; } = false;

    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    // Foreign keys
    public List<Quote> Quotes { get; set; }
    public List<ButtonGamePress> ButtonGamePresses { get; set; }
    public List<UserActivity> UserActivity { get; set; }
    public List<UserLevels> UserLevels { get; set; }
}
