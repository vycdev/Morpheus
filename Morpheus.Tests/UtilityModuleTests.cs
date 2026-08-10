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
    [InlineData("5 minutes remind me in 2 hours", "remind me in 2 hours")]
    [InlineData("5 days and 3 hours", null)]
    public void ExtractReminderText_RemovesDurationSeparators(string input, string? expected)
    {
        Assert.Equal(expected, UtilityModule.ExtractReminderText(input));
    }

    [Theory]
    [InlineData("5 minutes remind me in 2 hours", 300)]
    [InlineData("5 days and 3 hours call Alice", 442800)]
    [InlineData("5 days, 3 hours call Alice", 442800)]
    [InlineData("1 hour; 30 minutes check the oven", 5400)]
    [InlineData("7 weeks 2 minutes check the calendar", 4233720)]
    public void TryParseReminderDuration_OnlyCountsLeadingDuration(string input, double expectedSeconds)
    {
        bool parsed = UtilityModule.TryParseReminderDuration(input, out double totalSeconds);

        Assert.True(parsed);
        Assert.Equal(expectedSeconds, totalSeconds);
    }

    [Fact]
    public void TryParseReminderDuration_RejectsTextBeforeDuration()
    {
        bool parsed = UtilityModule.TryParseReminderDuration("Remind me in 2 hours", out double totalSeconds);

        Assert.False(parsed);
        Assert.Equal(0, totalSeconds);
    }
}
