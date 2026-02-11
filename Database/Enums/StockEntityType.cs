using System.ComponentModel.DataAnnotations;

namespace Morpheus.Database.Enums;

public enum StockEntityType
{
    [Display(Name = "User")]
    User = 1,

    [Display(Name = "Guild")]
    Guild = 2,

    [Display(Name = "Channel")]
    Channel = 3
}
