using Discord.Commands;
using System.Diagnostics;

namespace Morpheus.Attributes;

public class RateLimitData
{
    public DateTime StartTime { get; set; }
    public int Count { get; set; }
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
        if (_rateLimitData.TryGetValue(key, out RateLimitData? data))
        {
            TimeSpan elapsed = DateTime.UtcNow - data.StartTime;
            if (elapsed < TimeSpan.FromSeconds(seconds))
            {
                if (data.Count >= uses)
                {
                    TimeSpan timeLeft = TimeSpan.FromSeconds(seconds) - elapsed;
                    return Task.FromResult(PreconditionResult.FromError(
                        $"Command is on cooldown. Try again in {timeLeft.TotalSeconds:F0} seconds."));
                }
                else
                {
                    data.Count++;
                    return Task.FromResult(PreconditionResult.FromSuccess());
                }
            }
            else
            {
                // Reset the period if the cooldown has expired.
                data.StartTime = DateTime.UtcNow;
                data.Count = 1;
                return Task.FromResult(PreconditionResult.FromSuccess());
            }
        }
        else
        {
            // First execution for this user/command.
            _rateLimitData[key] = new RateLimitData { StartTime = DateTime.UtcNow, Count = 1 };
            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
