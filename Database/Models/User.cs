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

    // Foreign keys
    public List<Quote> Quotes { get; set; }
}
