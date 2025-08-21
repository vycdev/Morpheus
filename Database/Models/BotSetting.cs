using System;

namespace Morpheus.Database.Models;

public class BotSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
    public DateTime UpdateDate { get; set; } = DateTime.UtcNow;
}
