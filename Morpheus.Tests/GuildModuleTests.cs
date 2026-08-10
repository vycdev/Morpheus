using Morpheus.Modules;

namespace Morpheus.Tests;

public class GuildModuleTests
{
    [Theory]
    [InlineData("m!", "m!")]
    [InlineData("?", "?")]
    [InlineData("abc", "abc")]
    public void TryValidatePrefix_AcceptsPrintablePrefixes(string value, string expected)
    {
        Assert.True(GuildModule.TryValidatePrefix(value, out string prefix));
        Assert.Equal(expected, prefix);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcd")]
    [InlineData("m !")]
    [InlineData("m\n")]
    public void TryValidatePrefix_RejectsWhitespaceAndInvalidLengths(string? value)
    {
        Assert.False(GuildModule.TryValidatePrefix(value, out _));
    }
}
