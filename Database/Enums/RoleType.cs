using System.ComponentModel.DataAnnotations;

namespace Morpheus.Database.Enums;
public enum RoleType
{
    [Display(Name = "Top 1% Most Active", Description = "0xfc291e")] // Red
    TopOnePercent = 1,

    [Display(Name = "Top 5% Most Active", Description = "0x1e98fc")] // Blue
    TopFivePercent = 2,

    [Display(Name = "Top 10% Most Active", Description = "0x3ee673")] // Green
    TopTenPercent = 3,

    [Display(Name = "Top 20% Most Active", Description = "0x9e35db")] // Purple
    TopTwentyPercent = 4,

    [Display(Name = "Top 30% Most Active", Description = "0xf75528")] // Orange
    TopThirtyPercent = 5
}
