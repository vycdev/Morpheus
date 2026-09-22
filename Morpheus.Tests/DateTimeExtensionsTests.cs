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
}
