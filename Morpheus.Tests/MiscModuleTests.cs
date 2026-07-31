using System.Globalization;
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
    [InlineData("CHRISTMAS", "christmas")]
    [InlineData("CC BIRTHDAY", "cc birthday")]
    public void NormalizeTimeUntilEventName_NormalizesCase(string eventName, string expected)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            Assert.Equal(expected, MiscModule.NormalizeTimeUntilEventName(eventName));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
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
}
