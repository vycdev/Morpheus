using Discord.Commands;
using System.Diagnostics;

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
}

/// <summary>
/// Rate limit attribute for commands.
/// </summary>
/// <param name="uses"></param>
/// <param name="seconds"></param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RateLimitAttribute(int uses, int seconds) : PreconditionAttribute
{
    // A simple dictionary to track the last usage per user and command.
    private static readonly Dictionary<(ulong, string), RateLimitData> _rateLimitData = [];
    private static readonly object _rateLimitLock = new();

    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        // Skip cooldown in debugging mode
        if (Debugger.IsAttached)
            return Task.FromResult(PreconditionResult.FromSuccess());

        return ApplyRateLimit(context, command);
    }

    private Task<PreconditionResult> ApplyRateLimit(ICommandContext context, CommandInfo command)
    {
        (ulong Id, string Name) key = (context.User.Id, command.Name);

        RateLimitDecision decision;

        lock (_rateLimitLock)
        {
            decision = RateLimitPolicy.Apply(
                _rateLimitData,
                key,
                uses,
                TimeSpan.FromSeconds(seconds),
                DateTime.UtcNow);
        }

        if (decision.IsAllowed)
            return Task.FromResult(PreconditionResult.FromSuccess());

        return Task.FromResult(PreconditionResult.FromError(
            $"Command is on cooldown. Try again in {decision.RetryAfter.TotalSeconds:F0} seconds."));
    }
}
