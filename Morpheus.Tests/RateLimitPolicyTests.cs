using Morpheus.Attributes;

namespace Morpheus.Tests;

public class RateLimitPolicyTests
{
    private static readonly DateTime Now = new(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Apply_AllowsFirstUseAndCreatesWindow()
    {
        Dictionary<(ulong UserId, string CommandName), RateLimitData> state = [];
        (ulong UserId, string CommandName) key = (42, "level");

        RateLimitDecision decision = RateLimitPolicy.Apply(state, key, uses: 2, TimeSpan.FromSeconds(10), Now);

        Assert.True(decision.IsAllowed);
        Assert.Equal(1, state[key].Count);
        Assert.Equal(Now, state[key].StartTime);
    }

    [Fact]
    public void Apply_DeniesAfterAllowedUsesWithinWindow()
    {
        Dictionary<(ulong UserId, string CommandName), RateLimitData> state = [];
        (ulong UserId, string CommandName) key = (42, "level");

        Assert.True(RateLimitPolicy.Apply(state, key, uses: 2, TimeSpan.FromSeconds(10), Now).IsAllowed);
        Assert.True(RateLimitPolicy.Apply(state, key, uses: 2, TimeSpan.FromSeconds(10), Now.AddSeconds(1)).IsAllowed);
        RateLimitDecision denied = RateLimitPolicy.Apply(state, key, uses: 2, TimeSpan.FromSeconds(10), Now.AddSeconds(3));

        Assert.False(denied.IsAllowed);
        Assert.Equal(TimeSpan.FromSeconds(7), denied.RetryAfter);
        Assert.Equal(2, state[key].Count);
    }

    [Fact]
    public void Apply_ResetsWindowAfterPeriodExpires()
    {
        Dictionary<(ulong UserId, string CommandName), RateLimitData> state = [];
        (ulong UserId, string CommandName) key = (42, "level");

        Assert.True(RateLimitPolicy.Apply(state, key, uses: 1, TimeSpan.FromSeconds(10), Now).IsAllowed);
        Assert.False(RateLimitPolicy.Apply(state, key, uses: 1, TimeSpan.FromSeconds(10), Now.AddSeconds(5)).IsAllowed);
        RateLimitDecision afterReset = RateLimitPolicy.Apply(state, key, uses: 1, TimeSpan.FromSeconds(10), Now.AddSeconds(10));

        Assert.True(afterReset.IsAllowed);
        Assert.Equal(1, state[key].Count);
        Assert.Equal(Now.AddSeconds(10), state[key].StartTime);
    }
}
