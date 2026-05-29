using System.Data;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Npgsql;

namespace Morpheus.Services;

public class ActivityLevelService(DB dbContext)
{
    private const double UserEmaAlpha = 2.0 / (500.0 + 1.0);

    public async Task<ActivityLevelUpdateResult> RecordActivityAsync(UserActivity activity)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                UserLevels? userLevel = await dbContext.UserLevels
                    .FromSqlInterpolated($"""
                        SELECT *
                        FROM "UserLevels"
                        WHERE "UserId" = {activity.UserId}
                          AND "GuildId" = {activity.GuildId}
                        FOR UPDATE
                        """)
                    .SingleOrDefaultAsync();

                int previousLevel = userLevel?.Level ?? 0;

                if (userLevel == null)
                {
                    userLevel = new UserLevels
                    {
                        UserId = activity.UserId,
                        GuildId = activity.GuildId
                    };

                    ApplyActivityToUserLevel(userLevel, activity);
                    dbContext.UserLevels.Add(userLevel);

                    // Save the new level row before adding activity, so a unique race can retry cleanly.
                    await dbContext.SaveChangesAsync();
                }
                else
                {
                    ApplyActivityToUserLevel(userLevel, activity);
                }

                dbContext.UserActivity.Add(activity);
                await dbContext.SaveChangesAsync();

                await transaction.CommitAsync();

                return new ActivityLevelUpdateResult(previousLevel, userLevel.Level, userLevel.TotalXp);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && attempt == 0)
            {
                await transaction.RollbackAsync();
                dbContext.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("Failed to record activity after retrying a concurrent user level insert.");
    }

    public static int CalculateLevel(long xp)
    {
        return (int)Math.Pow(Math.Log10((xp + 111) / 111), 5.0243);
    }

    public static int CalculateXp(int level)
    {
        if (level <= 0)
            return 0;

        long estimate = Math.Max(0, (long)Math.Floor(111 * Math.Pow(10, Math.Pow(level, 1.0 / 5.0243)) - 111));
        long lower = Math.Max(0, estimate - 111);
        long upper = Math.Max(estimate + 111, 1);

        while (CalculateLevel(upper) < level)
        {
            lower = upper;
            upper *= 2;

            if (upper > int.MaxValue)
                throw new OverflowException($"Level {level} requires more XP than can be represented as an integer.");
        }

        if (upper > int.MaxValue)
            throw new OverflowException($"Level {level} requires more XP than can be represented as an integer.");

        while (lower + 1 < upper)
        {
            long midpoint = lower + ((upper - lower) / 2);

            if (CalculateLevel(midpoint) >= level)
                upper = midpoint;
            else
                lower = midpoint;
        }

        return (int)upper;
    }

    private static void ApplyActivityToUserLevel(UserLevels userLevel, UserActivity activity)
    {
        userLevel.TotalXp += activity.XpGained;
        userLevel.Level = CalculateLevel(userLevel.TotalXp);

        int previousMessageCount = userLevel.UserMessageCount;
        double previousAverageLength = userLevel.UserAverageMessageLength;
        double previousEmaLength = userLevel.UserAverageMessageLengthEma;

        int newMessageCount = previousMessageCount + 1;
        double messageLength = activity.MessageLength;

        userLevel.UserMessageCount = newMessageCount;
        userLevel.UserAverageMessageLength = previousMessageCount > 0
            ? ((previousAverageLength * previousMessageCount) + messageLength) / newMessageCount
            : messageLength;
        userLevel.UserAverageMessageLengthEma = previousEmaLength <= 0.0
            ? messageLength
            : (1.0 - UserEmaAlpha) * previousEmaLength + UserEmaAlpha * messageLength;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}

public sealed record ActivityLevelUpdateResult(int PreviousLevel, int NewLevel, int TotalXp)
{
    public bool LevelChanged => NewLevel != PreviousLevel;
}
