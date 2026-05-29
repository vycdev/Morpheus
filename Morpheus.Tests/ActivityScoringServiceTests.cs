using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class ActivityScoringServiceTests
{
    private static readonly DateTime Now = new(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ScoreMessage_FirstMessage_GrantsBaseLengthXpAndInitializesGuildStats()
    {
        ActivityScoringResult score = ActivityScoringService.ScoreMessage(
            "hello",
            Now,
            previousUserActivityInGuild: null,
            previousActivityInGuild: null,
            recentForSimilarity: Array.Empty<ActivitySimilaritySample>());

        Assert.Equal(5, score.XpGained);
        Assert.Equal(5, score.MessageLength);
        Assert.Equal(5, score.GuildAverageMessageLength);
        Assert.Equal(1, score.GuildMessageCount);
        Assert.False(string.IsNullOrWhiteSpace(score.MessageHash));
    }

    [Fact]
    public void ScoreMessage_RepeatedMessageWithinOneMinute_GrantsNoXp()
    {
        ActivityScoringResult previousScore = ActivityScoringService.ScoreMessage(
            "same repeated text",
            Now.AddSeconds(-30),
            previousUserActivityInGuild: null,
            previousActivityInGuild: null,
            recentForSimilarity: Array.Empty<ActivitySimilaritySample>());

        UserActivity previousUserActivity = new()
        {
            InsertDate = Now.AddSeconds(-30),
            MessageHash = previousScore.MessageHash,
            GuildAverageMessageLength = previousScore.GuildAverageMessageLength,
            GuildMessageCount = previousScore.GuildMessageCount
        };

        ActivityScoringResult score = ActivityScoringService.ScoreMessage(
            "same repeated text",
            Now,
            previousUserActivity,
            previousUserActivity,
            recentForSimilarity: Array.Empty<ActivitySimilaritySample>());

        Assert.Equal(0, score.XpGained);
    }

    [Fact]
    public void ScoreMessage_NearDuplicateSimHash_GrantsNoXp()
    {
        const string content = "this message has enough normalized text to compare";
        ActivityScoringResult previousScore = ActivityScoringService.ScoreMessage(
            content,
            Now.AddMinutes(-5),
            previousUserActivityInGuild: null,
            previousActivityInGuild: null,
            recentForSimilarity: Array.Empty<ActivitySimilaritySample>());

        ActivityScoringResult score = ActivityScoringService.ScoreMessage(
            content,
            Now,
            previousUserActivityInGuild: null,
            previousActivityInGuild: null,
            recentForSimilarity:
            [
                new ActivitySimilaritySample(previousScore.MessageSimHash, previousScore.NormalizedLength)
            ]);

        Assert.Equal(0, score.XpGained);
    }

    [Fact]
    public void ScoreMessage_FastLongMessage_GrantsNoXp()
    {
        UserActivity previousUserActivity = new()
        {
            InsertDate = Now.AddSeconds(-1),
            MessageHash = "different-message"
        };

        ActivityScoringResult score = ActivityScoringService.ScoreMessage(
            new string('a', 100),
            Now,
            previousUserActivity,
            previousActivityInGuild: null,
            recentForSimilarity: Array.Empty<ActivitySimilaritySample>());

        Assert.Equal(0, score.XpGained);
    }

    [Fact]
    public void ScoreMessage_WithPreviousGuildActivity_UpdatesGuildLengthEma()
    {
        UserActivity previousGuildActivity = new()
        {
            GuildAverageMessageLength = 100.0,
            GuildMessageCount = 9
        };

        ActivityScoringResult score = ActivityScoringService.ScoreMessage(
            new string('x', 50),
            Now,
            previousUserActivityInGuild: null,
            previousGuildActivity,
            recentForSimilarity: Array.Empty<ActivitySimilaritySample>());

        const double alpha = 2.0 / (500.0 + 1.0);
        double expectedAverage = (1.0 - alpha) * 100.0 + alpha * 50.0;

        Assert.Equal(10, score.GuildMessageCount);
        Assert.Equal(expectedAverage, score.GuildAverageMessageLength, precision: 12);
    }
}
