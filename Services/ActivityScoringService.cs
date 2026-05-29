using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Utilities;
using Morpheus.Utilities.Text;
using System.IO.Hashing;
using System.Text;

namespace Morpheus.Services;

public class ActivityScoringService(DB dbContext)
{
    private const int RecentSimilaritySampleLimit = 200;
    private const double GuildMessageLengthEmaAlpha = 2.0 / (500.0 + 1.0);

    public async Task<UserActivity> CreateActivityAsync(
        int userId,
        int guildId,
        ulong discordChannelId,
        ulong discordMessageId,
        string messageContent,
        DateTime now)
    {
        int similarityWindowMinutes = Env.Get<int>("ACTIVITY_SIMILARITY_WINDOW_MINUTES", 10);
        DateTime similarityWindowStart = now.AddMinutes(-similarityWindowMinutes);

        UserActivity? previousUserActivityInGuild = await dbContext.UserActivity
            .Where(ua => ua.UserId == userId && ua.GuildId == guildId)
            .OrderByDescending(ua => ua.InsertDate)
            .FirstOrDefaultAsync();

        List<ActivitySimilaritySample> recentForSimilarity = await dbContext.UserActivity
            .Where(ua => ua.UserId == userId && ua.GuildId == guildId && ua.InsertDate >= similarityWindowStart)
            .OrderByDescending(ua => ua.InsertDate)
            .Select(ua => new ActivitySimilaritySample(ua.MessageSimHash, ua.NormalizedLength))
            .Take(RecentSimilaritySampleLimit)
            .ToListAsync();

        UserActivity? previousActivityInGuild = await dbContext.UserActivity
            .Where(ua => ua.GuildId == guildId)
            .OrderByDescending(ua => ua.InsertDate)
            .FirstOrDefaultAsync();

        ActivityScoringResult score = ScoreMessage(
            messageContent,
            now,
            previousUserActivityInGuild,
            previousActivityInGuild,
            recentForSimilarity);

        return new UserActivity
        {
            DiscordChannelId = discordChannelId,
            DiscordMessageId = discordMessageId,
            GuildId = guildId,
            InsertDate = now,
            MessageHash = score.MessageHash,
            UserId = userId,
            XpGained = score.XpGained,
            MessageLength = score.MessageLength,
            MessageSimHash = score.MessageSimHash,
            NormalizedLength = score.NormalizedLength,
            GuildAverageMessageLength = score.GuildAverageMessageLength,
            GuildMessageCount = score.GuildMessageCount
        };
    }

    public static ActivityScoringResult ScoreMessage(
        string messageContent,
        DateTime now,
        UserActivity? previousUserActivityInGuild,
        UserActivity? previousActivityInGuild,
        IReadOnlyCollection<ActivitySimilaritySample> recentForSimilarity)
    {
        string messageHash = Convert.ToBase64String(XxHash64.Hash(Encoding.UTF8.GetBytes(messageContent)));
        (ulong simHash, int normalizedLength) = SimHasher.ComputeSimHash(messageContent);

        double messageLengthXp = CalculateMessageLengthXp(messageContent.Length, previousActivityInGuild);
        double similarityPenaltySimple = CalculateSimpleSimilarityPenalty(messageHash, now, previousUserActivityInGuild);
        double speedPenaltySimple = CalculateSimpleSpeedPenalty(now, previousUserActivityInGuild);
        double similarityPenaltyComplex = CalculateComplexSimilarityPenalty(simHash, normalizedLength, recentForSimilarity);
        double speedPenaltyComplex = CalculateComplexSpeedPenalty(messageContent.Length, now, previousUserActivityInGuild);

        int xp = (int)Math.Floor((1 + messageLengthXp)
            * similarityPenaltySimple
            * similarityPenaltyComplex
            * speedPenaltySimple
            * speedPenaltyComplex);

        (double guildAverageMessageLength, int guildMessageCount) = CalculateGuildMessageLengthStats(
            messageContent.Length,
            previousActivityInGuild);

        return new ActivityScoringResult(
            messageHash,
            simHash,
            normalizedLength,
            messageContent.Length,
            guildAverageMessageLength,
            guildMessageCount,
            xp);
    }

    private static double CalculateMessageLengthXp(int messageLength, UserActivity? previousActivityInGuild)
    {
        double averageLength = previousActivityInGuild?.GuildAverageMessageLength ?? 0.0;
        double ratio = 1.0;

        if (averageLength > 0.0)
            ratio = messageLength / averageLength;

        if (ratio < 0.0)
            ratio = 0.0;
        else if (ratio > 100.0)
            ratio = 100.0;

        const double bonus = 4.0;
        const double curve = 0.025;
        double denominator = Math.Log(1.0 + curve);

        return denominator > 0.0
            ? bonus * Math.Log(1.0 + (curve * ratio)) / denominator
            : bonus * ratio;
    }

    private static double CalculateSimpleSimilarityPenalty(
        string messageHash,
        DateTime now,
        UserActivity? previousUserActivityInGuild)
    {
        return previousUserActivityInGuild?.MessageHash == messageHash
               && Math.Abs((now - previousUserActivityInGuild.InsertDate).TotalSeconds) < 60
            ? 0.0
            : 1.0;
    }

    private static double CalculateSimpleSpeedPenalty(DateTime now, UserActivity? previousUserActivityInGuild)
    {
        if (previousUserActivityInGuild == null)
            return 1.0;

        double secondsSincePrevious = (now - previousUserActivityInGuild.InsertDate).TotalSeconds;

        if (secondsSincePrevious < 0.0)
            secondsSincePrevious = 0.0;
        else if (secondsSincePrevious > 5.0)
            secondsSincePrevious = 5.0;

        const double curve = 9.0;
        return Math.Log(1.0 + curve * secondsSincePrevious) / Math.Log(1.0 + curve * 5.0);
    }

    private static double CalculateComplexSimilarityPenalty(
        ulong simHash,
        int normalizedLength,
        IReadOnlyCollection<ActivitySimilaritySample> recentForSimilarity)
    {
        if (normalizedLength < 12 || recentForSimilarity.Count == 0 || simHash == 0UL)
            return 1.0;

        double maxSimilarity = 0.0;

        foreach (ActivitySimilaritySample previous in recentForSimilarity)
        {
            if (previous.MessageSimHash == 0UL || previous.NormalizedLength < 12)
                continue;

            int hammingDistance = SimHasher.HammingDistance(simHash, previous.MessageSimHash);
            double similarity = 1.0 - (hammingDistance / 64.0);

            if (similarity > maxSimilarity)
                maxSimilarity = similarity;
        }

        if (maxSimilarity >= 0.92)
            return 0.0;

        if (maxSimilarity >= 0.85)
            return 0.25;

        return 1.0;
    }

    private static double CalculateComplexSpeedPenalty(
        int messageLength,
        DateTime now,
        UserActivity? previousUserActivityInGuild)
    {
        if (previousUserActivityInGuild == null || messageLength < 50)
            return 1.0;

        double minutesSincePrevious = Math.Max((now - previousUserActivityInGuild.InsertDate).TotalMinutes, 1e-6);
        double wordsPerMinute = messageLength / minutesSincePrevious / 5.0;

        if (wordsPerMinute <= 200.0)
            return 1.0;

        if (wordsPerMinute >= 300.0)
            return 0.0;

        double x = (wordsPerMinute - 200.0) / 100.0;
        double decrease = Math.Log(1.0 + 9.0 * x) / Math.Log(10.0);
        return 1.0 - decrease;
    }

    private static (double AverageMessageLength, int MessageCount) CalculateGuildMessageLengthStats(
        int messageLength,
        UserActivity? previousActivityInGuild)
    {
        if (previousActivityInGuild == null)
            return (messageLength, 1);

        int messageCount = previousActivityInGuild.GuildMessageCount + 1;
        double previousAverage = previousActivityInGuild.GuildAverageMessageLength;
        double averageMessageLength = previousAverage <= 0.0
            ? messageLength
            : (1.0 - GuildMessageLengthEmaAlpha) * previousAverage + GuildMessageLengthEmaAlpha * messageLength;

        return (averageMessageLength, messageCount);
    }
}

public sealed record ActivitySimilaritySample(ulong MessageSimHash, int NormalizedLength);

public sealed record ActivityScoringResult(
    string MessageHash,
    ulong MessageSimHash,
    int NormalizedLength,
    int MessageLength,
    double GuildAverageMessageLength,
    int GuildMessageCount,
    int XpGained);
