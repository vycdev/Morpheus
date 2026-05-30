using Morpheus.Services;

namespace Morpheus.Tests;

public class ActivityGraphServiceTests
{
    [Fact]
    public void ParseDaysString_ClampsPresetDaysForNonOwner()
    {
        ActivityGraphParseResult result = ActivityGraphService.ParseDaysString(
            "past180days",
            isOwner: false,
            maxDays: 90);

        Assert.True(result.Success);
        Assert.Equal(90, result.Days);
        Assert.Null(result.ExplicitStart);
    }

    [Fact]
    public void ParseDaysString_DoesNotClampPresetDaysForOwner()
    {
        ActivityGraphParseResult result = ActivityGraphService.ParseDaysString(
            "past180days",
            isOwner: true,
            maxDays: 90);

        Assert.True(result.Success);
        Assert.Equal(180, result.Days);
    }

    [Fact]
    public void ParseDaysString_ExpandsShortDateRangeToSevenDays()
    {
        ActivityGraphParseResult result = ActivityGraphService.ParseDaysString(
            "2026-05-01..2026-05-02",
            isOwner: false,
            maxDays: 90);

        Assert.True(result.Success);
        Assert.Equal(7, result.Days);
        Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), result.ExplicitStart);
    }

    [Fact]
    public void ParseDaysString_RejectsTooLargeDateRangeForNonOwner()
    {
        ActivityGraphParseResult result = ActivityGraphService.ParseDaysString(
            "2026-01-01..2026-04-30",
            isOwner: false,
            maxDays: 90);

        Assert.False(result.Success);
        Assert.Equal("Date range exceeds maximum of 90 days.", result.ErrorMessage);
    }

    [Fact]
    public void ResolveRange_UsesTrailingInclusiveWindowWhenNoExplicitStartExists()
    {
        ActivityGraphParseResult parse = ActivityGraphParseResult.Valid(days: 7, explicitStart: null);

        ActivityGraphRange range = ActivityGraphService.ResolveRange(
            parse,
            utcNow: new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 5, 24, 0, 0, 0, DateTimeKind.Utc), range.Start);
        Assert.Equal(new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc), range.End);
    }

    [Fact]
    public void RollingAverage_UsesTrailingWindow()
    {
        Dictionary<string, List<int>> series = new()
        {
            ["user"] = [10, 20, 30, 40]
        };

        Dictionary<string, List<int>> result = ActivityGraphService.RollingAverage(series, windowDays: 3);

        Assert.Equal([10, 15, 20, 30], result["user"]);
    }

    [Fact]
    public void BuildDailyValues_FillsMissingDaysWithZero()
    {
        DateTime start = new(2026, 5, 24, 0, 0, 0, DateTimeKind.Utc);
        Dictionary<DateTime, int> values = new()
        {
            [start] = 2,
            [start.AddDays(2)] = 5
        };

        List<int> daily = ActivityGraphService.BuildDailyValues(values, start, days: 4);

        Assert.Equal([2, 0, 5, 0], daily);
    }

    [Fact]
    public void BuildCumulativeValues_AddsBaselineBeforeDailyValues()
    {
        List<int> cumulative = ActivityGraphService.BuildCumulativeValues([2, 0, 5], baseline: 100);

        Assert.Equal([102, 102, 107], cumulative);
    }

    [Fact]
    public void BuildUserGraphMessage_FormatsGlobalRollingRange()
    {
        ActivityGraphRange range = new(
            Days: 7,
            Start: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            ExplicitStart: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

        string message = ActivityGraphService.BuildUserGraphMessage(
            seriesCount: 2,
            range,
            global: true,
            cumulative: false,
            rollingWindowDays: 7);

        Assert.Equal("Top 2 users global 7-day rolling average activity from 2026-05-01 to 2026-05-07 (7 days)", message);
    }
}
