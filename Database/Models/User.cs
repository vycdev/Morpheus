﻿using System.ComponentModel.DataAnnotations;
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

    // Foreign keys
    public List<Quote> Quotes { get; set; }
    public List<ButtonGamePress> ButtonGamePresses { get; set; }
    public List<UserActivity> UserActivity { get; set; }
    public List<UserLevels> UserLevels { get; set; }
}
