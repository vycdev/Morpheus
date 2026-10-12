using Discord;
using Morpheus.Database.Enums;
using Morpheus.Utilities.Extensions;

namespace Morpheus.Tests;

public class ActivityRoleMetadataTests
{
    [Theory]
    [InlineData(ActivityRoleType.TopOnePercent, "Top 1% Most Active", 0xfc291e)]
    [InlineData(ActivityRoleType.TopFivePercent, "Top 5% Most Active", 0x1e98fc)]
    [InlineData(ActivityRoleType.TopTenPercent, "Top 10% Most Active", 0x3ee673)]
    [InlineData(ActivityRoleType.TopTwentyPercent, "Top 20% Most Active", 0x9e35db)]
    [InlineData(ActivityRoleType.TopThirtyPercent, "Top 30% Most Active", 0xf75528)]
    public void ActivityRoles_HaveExpectedDiscordNamesAndColors(ActivityRoleType role, string name, uint color)
    {
        Assert.Equal(name, role.GetDisplayName());
        Assert.Equal(new Color(color), role.GetDiscordColor());
    }

    [Fact]
    public void UndefinedActivityRole_HasNoDisplayMetadata()
    {
        ActivityRoleType role = (ActivityRoleType)0;

        Assert.Null(role.GetDisplayName());
        Assert.Null(role.GetDiscordColor());
    }
}
