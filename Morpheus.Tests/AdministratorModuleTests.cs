using Morpheus.Modules;

namespace Morpheus.Tests;

public class AdministratorModuleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-user-id")]
    public void TryParseOwnerId_RejectsMissingOrInvalidValues(string? value)
    {
        Assert.False(AdministratorModule.TryParseOwnerId(value, out _));
    }

    [Fact]
    public void TryParseOwnerId_AcceptsValidDiscordUserId()
    {
        Assert.True(AdministratorModule.TryParseOwnerId("123456789012345678", out ulong ownerId));
        Assert.Equal(123456789012345678UL, ownerId);
    }
}