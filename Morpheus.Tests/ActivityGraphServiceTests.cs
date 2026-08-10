using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class ActivityGraphServiceTests
{
    [Fact]
    public async Task BuildUserActivityGraphAsync_PreservesUsersWithMatchingNames()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        Guild guild = new() { DiscordId = 100, Name = "Test guild" };
        User firstUser = new() { DiscordId = 1001, Username = "matching-name" };
        User secondUser = new() { DiscordId = 1002, Username = "matching-name" };
        db.AddRange(guild, firstUser, secondUser);
        await db.SaveChangesAsync();

        DateTime start = new(2026, 5, 24, 0, 0, 0, DateTimeKind.Utc);
        db.UserActivity.AddRange(
            new UserActivity
            {
                UserId = firstUser.Id,
                GuildId = guild.Id,
                DiscordChannelId = 2001,
                DiscordMessageId = 3001,
                XpGained = 20,
                InsertDate = start
            },
            new UserActivity
            {
                UserId = secondUser.Id,
                GuildId = guild.Id,
                DiscordChannelId = 2001,
                DiscordMessageId = 3002,
                XpGained = 10,
                InsertDate = start
            });
        await db.SaveChangesAsync();

        ActivityGraphService service = new(db);
        ActivityGraphBuildResult result = await service.BuildUserActivityGraphAsync(
            new ActivityGraphRange(Days: 7, Start: start, ExplicitStart: start),
            guildId: guild.Id,
            global: false,
            mentionedDiscordIds: [],
            cumulative: false,
            rollingWindowDays: null);

        Assert.Equal(2, result.Series.Count);
        Assert.Equal(20, result.Series["matching-name"][0]);
        Assert.Equal(10, result.Series["matching-name (1002)"][0]);
    }

    [Fact]
    public async Task BuildUserActivityGraphAsync_ClampsDailyTotalsThatExceedIntRange()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        Guild guild = new() { DiscordId = 200, Name = "Overflow guild" };
        User user = new() { DiscordId = 2001, Username = "high-xp" };
        db.AddRange(guild, user);
        await db.SaveChangesAsync();

        DateTime start = new(2026, 5, 24, 0, 0, 0, DateTimeKind.Utc);
        db.UserActivity.AddRange(
            new UserActivity
            {
                UserId = user.Id,
                GuildId = guild.Id,
                DiscordChannelId = 2001,
                DiscordMessageId = 4001,
                XpGained = 1_500_000_000,
                InsertDate = start
            },
            new UserActivity
            {
                UserId = user.Id,
                GuildId = guild.Id,
                DiscordChannelId = 2001,
                DiscordMessageId = 4002,
                XpGained = 1_500_000_000,
                InsertDate = start
            },
            new UserActivity
            {
                UserId = user.Id,
                GuildId = guild.Id,
                DiscordChannelId = 2001,
                DiscordMessageId = 4003,
                XpGained = -852_516_353,
                InsertDate = start.AddDays(1)
            });
        await db.SaveChangesAsync();

        ActivityGraphService service = new(db);
        ActivityGraphRange range = new(Days: 7, Start: start, ExplicitStart: start);
        ActivityGraphBuildResult result = await service.BuildUserActivityGraphAsync(
            range,
            guildId: guild.Id,
            global: false,
            mentionedDiscordIds: [],
            cumulative: false,
            rollingWindowDays: null);

        Assert.Equal(int.MaxValue, result.Series["high-xp"][0]);

        ActivityGraphBuildResult cumulative = await service.BuildUserActivityGraphAsync(
            range,
            guildId: guild.Id,
            global: false,
            mentionedDiscordIds: [],
            cumulative: true,
            rollingWindowDays: null);

        Assert.Equal([int.MaxValue, int.MaxValue], cumulative.Series["high-xp"][..2]);
    }

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
        Dictionary<DateTime, long> values = new()
        {
            [start] = 2,
            [start.AddDays(2)] = 5
        };

        List<long> daily = ActivityGraphService.BuildDailyValues(values, start, days: 4);

        Assert.Equal([2, 0, 5, 0], daily);
    }

    [Fact]
    public void BuildCumulativeValues_AddsBaselineBeforeDailyValues()
    {
        List<int> cumulative = ActivityGraphService.BuildCumulativeValues([2, 0, 5], baseline: 100);

        Assert.Equal([102, 102, 107], cumulative);
    }

    [Fact]
    public void BuildCumulativeValues_PreservesOverflowRemainderAcrossLaterValues()
    {
        List<int> cumulative = ActivityGraphService.BuildCumulativeValues(
            [100, -100],
            baseline: int.MaxValue);

        Assert.Equal([int.MaxValue, int.MaxValue], cumulative);
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
