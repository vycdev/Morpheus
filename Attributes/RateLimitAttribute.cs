using System.Diagnostics;
using Discord.Commands;
using Morpheus.Extensions;

namespace Morpheus.Attributes;

public class RateLimitData
{
    public DateTime StartTime { get; set; }
    public int Count { get; set; }
}

public sealed record RateLimitDecision(bool IsAllowed, TimeSpan RetryAfter)
{
    public static RateLimitDecision Allowed { get; } = new(true, TimeSpan.Zero);
}

public static class RateLimitPolicy
{
    public static RateLimitDecision Apply(
        IDictionary<(ulong UserId, string CommandName), RateLimitData> rateLimitData,
        (ulong UserId, string CommandName) key,
        int uses,
        TimeSpan period,
        DateTime now)
    {
        if (rateLimitData.TryGetValue(key, out RateLimitData? data))
        {
            TimeSpan elapsed = now - data.StartTime;
            if (elapsed < period)
            {
                if (data.Count >= uses)
                    return new RateLimitDecision(false, period - elapsed);

                data.Count++;
                return RateLimitDecision.Allowed;
            }

            data.StartTime = now;
            data.Count = 1;
            return RateLimitDecision.Allowed;
        }

        rateLimitData[key] = new RateLimitData { StartTime = now, Count = 1 };
        return RateLimitDecision.Allowed;
    }

    public static RateLimitDecision Check(
        IReadOnlyDictionary<(ulong UserId, string CommandName), RateLimitData> rateLimitData,
        (ulong UserId, string CommandName) key,
        int uses,
        TimeSpan period,
        DateTime now)
    {
        if (!rateLimitData.TryGetValue(key, out RateLimitData? data))
            return RateLimitDecision.Allowed;

        TimeSpan elapsed = now - data.StartTime;
        return elapsed < period && data.Count >= uses
            ? new RateLimitDecision(false, period - elapsed)
            : RateLimitDecision.Allowed;
    }
}

/// <summary>Rate limit attribute for commands.</summary>
public class RateLimitAttribute(int uses, int seconds) : PreconditionAttribute
{
    private static readonly Dictionary<(ulong, string), RateLimitData> RateLimitData = [];
    private static readonly object RateLimitLock = new();

    public override Task<PreconditionResult> CheckPermissionsAsync(
        ICommandContext context,
        CommandInfo command,
        IServiceProvider services)
    {
        if (Debugger.IsAttached)
            return Task.FromResult(PreconditionResult.FromSuccess());

        (ulong Id, string Name) key = (context.User.Id, command.Name);
        RateLimitDecision decision;

        lock (RateLimitLock)
        {
            decision = context is SocketCommandContextExtended { IsValidation: true }
                ? RateLimitPolicy.Check(
                    RateLimitData,
                    key,
                    uses,
                    TimeSpan.FromSeconds(seconds),
                    DateTime.UtcNow)
                : RateLimitPolicy.Apply(
                    RateLimitData,
                    key,
                    uses,
                    TimeSpan.FromSeconds(seconds),
                    DateTime.UtcNow);
        }

        return Task.FromResult(decision.IsAllowed
            ? PreconditionResult.FromSuccess()
            : PreconditionResult.FromError(
                $"Command is on cooldown. Try again in {decision.RetryAfter.TotalSeconds:F0} seconds."));
    }
}
