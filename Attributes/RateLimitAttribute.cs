using Discord.Commands;

namespace Morpheus.Attributes;

public class RateLimitData
{
    public DateTime StartTime { get; set; }
    public int Count { get; set; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RateLimitAttribute : PreconditionAttribute
{
    private readonly int _uses;
    private readonly int _seconds;
    // A simple dictionary to track the last usage per user and command.
    private static readonly Dictionary<(ulong, string), RateLimitData> _rateLimitData = new();

    public RateLimitAttribute(int uses, int seconds)
    {
        _uses = uses;
        _seconds = seconds;
    }

    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        return ApplyRateLimit(context, command);
    }

    private Task<PreconditionResult> ApplyRateLimit(ICommandContext context, CommandInfo command)
    {
        var key = (context.User.Id, command.Name);
        if (_rateLimitData.TryGetValue(key, out var data))
        {
            var elapsed = DateTime.UtcNow - data.StartTime;
            if (elapsed < TimeSpan.FromSeconds(_seconds))
            {
                if (data.Count >= _uses)
                {
                    var timeLeft = TimeSpan.FromSeconds(_seconds) - elapsed;
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
