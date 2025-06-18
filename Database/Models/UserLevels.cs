using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morpheus.Database.Models;

public class UserLevels
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }
    
    [Required]
    public int GuildId { get; set; }
    
    public int Level { get; set; } = 0;
    public int TotalXp { get; set; } = 0;

    // Foreign keys
    [ForeignKey("UserId")]
    public User User { get; set; }

    [ForeignKey("GuildId")]
    public Guild Guild { get; set; }

}
