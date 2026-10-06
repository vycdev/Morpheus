using Morpheus.Utilities.Extensions;

namespace Morpheus.Tests;

public class DateTimeExtensionsTests
{
    [Fact]
    public void GetAccurateTimeSpan_HandlesMaximumEndDate()
    {
        DateTime start = new(9999, 11, 30, 23, 59, 59, DateTimeKind.Utc);

        string result = start.GetAccurateTimeSpan(DateTime.MaxValue);

        Assert.Equal("1 months, 1 days, 999 milliseconds", result);
    }

    [Fact]
    public void GetAccurateTimeSpan_HandlesEqualMaximumDates()
    {
        string result = DateTime.MaxValue.GetAccurateTimeSpan(DateTime.MaxValue);

        Assert.Equal("0 milliseconds", result);
    }

    [Theory]
    [InlineData(2024, 2, 29, 2025, 2, 28, "1 years")]
    [InlineData(2024, 2, 29, 2025, 3, 1, "1 years, 1 days")]
    [InlineData(2024, 1, 31, 2024, 3, 1, "1 months, 1 days")]
    [InlineData(2023, 1, 31, 2023, 3, 1, "1 months, 1 days")]
    public void GetAccurateTimeSpan_HandlesLeapDaysAndShortMonths(
        int startYear, int startMonth, int startDay,
        int endYear, int endMonth, int endDay, string expected)
    {
        DateTime start = new(startYear, startMonth, startDay, 0, 0, 0, DateTimeKind.Utc);
        DateTime end = new(endYear, endMonth, endDay, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expected, start.GetAccurateTimeSpan(end));
    }

    [Fact]
    public void GetAccurateTimeSpan_RejectsEndBeforeStart()
    {
        DateTime start = new(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        Assert.Throws<ArgumentException>(() => start.GetAccurateTimeSpan(start.AddTicks(-1)));
    }
}
