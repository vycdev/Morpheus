using Morpheus.Modules;

namespace Morpheus.Tests;

public class MiscModuleTests
{
    [Theory]
    [InlineData(2026, 12, 24, 12, 12, 25, 2026)]
    [InlineData(2026, 12, 25, 0, 12, 25, 2026)]
    [InlineData(2026, 12, 25, 12, 12, 25, 2027)]
    [InlineData(2026, 11, 1, 12, 10, 31, 2027)]
    [InlineData(2026, 9, 3, 12, 9, 2, 2027)]
    public void GetNextAnnualEventDate_ReturnsNextOccurrence(
        int currentYear,
        int currentMonth,
        int currentDay,
        int currentHour,
        int eventMonth,
        int eventDay,
        int expectedYear)
    {
        DateTime now = new(currentYear, currentMonth, currentDay, currentHour, 0, 0, DateTimeKind.Utc);

        DateTime result = MiscModule.GetNextAnnualEventDate(now, eventMonth, eventDay);

        Assert.Equal(new DateTime(expectedYear, eventMonth, eventDay, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Theory]
    [InlineData("ROCK", "rock")]
    [InlineData("PaPeR", "paper")]
    [InlineData("ScIsSoRs", "scissors")]
    public void NormalizeRockPaperScissorsChoice_NormalizesMixedCase(string choice, string expected)
    {
        Assert.Equal(expected, MiscModule.NormalizeRockPaperScissorsChoice(choice));
    }

    [Fact]
    public void GenerateRandomNumber_SupportsIntMaxValueAsInclusiveUpperBound()
    {
        int result = MiscModule.GenerateRandomNumber(int.MaxValue, int.MaxValue);

        Assert.Equal(int.MaxValue, result);
    }

    [Theory]
    [InlineData("1d6", 1, 6)]
    [InlineData("2D20", 2, 20)]
    public void TryParseDiceInput_AcceptsEitherSeparatorCase(string input, int expectedCount, int expectedSides)
    {
        bool parsed = MiscModule.TryParseDiceInput(input, out int count, out int sides);

        Assert.True(parsed);
        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedSides, sides);
    }

    [Theory]
    [InlineData("1d6D8")]
    [InlineData("1D")]
    [InlineData("D6")]
    [InlineData("0D6")]
    [InlineData("1D1")]
    public void TryParseDiceInput_RejectsMalformedOrOutOfRangeInput(string input)
    {
        Assert.False(MiscModule.TryParseDiceInput(input, out _, out _));
    }
}