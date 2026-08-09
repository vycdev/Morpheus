using Morpheus.Modules;

namespace Morpheus.Tests;

public class UtilityModuleTests
{
    [Fact]
    public void TryGetReferencedMessageId_ReturnsFalseWhenReferenceIsMissing()
    {
        bool found = UtilityModule.TryGetReferencedMessageId(null, out ulong messageId);

        Assert.False(found);
        Assert.Equal(0UL, messageId);
    }

    [Fact]
    public void TryGetReferencedMessageId_ReturnsReferenceIdWhenPresent()
    {
        bool found = UtilityModule.TryGetReferencedMessageId(42UL, out ulong messageId);

        Assert.True(found);
        Assert.Equal(42UL, messageId);
    }

    [Fact]
    public void FormatPinTitle_UsesReferencedMessageAuthor()
    {
        string title = UtilityModule.FormatPinTitle("general", "OriginalAuthor");

        Assert.Equal("Pin in `#general` by OriginalAuthor", title);
    }

    [Theory]
    [InlineData("5 days and 3 hours @User Take a break", "@User Take a break")]
    [InlineData("5 days, 3 hours: Take a break", "Take a break")]
    [InlineData("5 days - Buy milk and eggs", "Buy milk and eggs")]
    [InlineData("5 days Remember Alice and Bob", "Remember Alice and Bob")]
    [InlineData("5 days and remember to buy milk", "and remember to buy milk")]
    [InlineData("5 days and 3 hours and remember to call Alice", "and remember to call Alice")]
    [InlineData("5 days, and remember to buy milk", "and remember to buy milk")]
    [InlineData("5 days and 3 hours", null)]
    public void ExtractReminderText_RemovesDurationSeparators(string input, string? expected)
    {
        Assert.Equal(expected, UtilityModule.ExtractReminderText(input));
    }
}
