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
    
    public string Name { get; set; }
    
    // Settings
    public string Prefix { get; set; } = "m!";

    public ulong WelcomeChannelId { get; set; } 

    public ulong PinsChannelId { get; set; }

    public ulong LevelUpMessagesChannelId { get; set; }
    public ulong LevelUpQuotesChannelId { get; set; }

    public bool LevelUpMessages { get; set; } = true;
    public bool LevelUpQuotes { get; set; } = true;
    public bool WelcomeMessages { get; set; } = true;

    public bool UseGlobalQuotes { get; set; } = true;
    public bool UseGlobalLevelUpMessages { get; set; } = true;

    public ulong QuotesApprovalChannelId { get; set; }
    public int QuoteAddRequiredApprovals { get; set; } = 5;
    public int QuoteRemoveRequiredApprovals { get; set; } = 5;

    public bool UseActivityRoles { get; set; } = false;
    
    public DateTime InsertDate { get; set; } = DateTime.UtcNow;

    // Foreign keys
    public List<User> Users { get; set; }
    public List<Quote> Quotes { get; set; }
}
