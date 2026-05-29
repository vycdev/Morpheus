using Morpheus.Database.Enums;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class ActivityServiceTests
{
    [Fact]
    public void BuildActivityRoleAssignments_UsesConfiguredPercentagesForFullGuild()
    {
        List<ActivityRoleCandidate> candidates = CreateCandidates(100);

        ActivityRoleAssignmentResult result = ActivityService.BuildActivityRoleAssignments(candidates);

        Assert.Single(result.UsersByRole[RoleType.TopOnePercent]);
        Assert.Equal(5, result.UsersByRole[RoleType.TopFivePercent].Count);
        Assert.Equal(10, result.UsersByRole[RoleType.TopTenPercent].Count);
        Assert.Equal(20, result.UsersByRole[RoleType.TopTwentyPercent].Count);
        Assert.Equal(30, result.UsersByRole[RoleType.TopThirtyPercent].Count);
    }

    [Fact]
    public void BuildActivityRoleAssignments_KeepsRolesCumulativeFromTopCandidate()
    {
        List<ActivityRoleCandidate> candidates = CreateCandidates(10);

        ActivityRoleAssignmentResult result = ActivityService.BuildActivityRoleAssignments(candidates);

        Assert.Equal(candidates.Take(3).Select(c => c.User.Id), result.UsersByRole[RoleType.TopThirtyPercent].Select(u => u.Id));
        Assert.Equal(candidates.Take(2).Select(c => c.User.Id), result.UsersByRole[RoleType.TopTwentyPercent].Select(u => u.Id));
        Assert.Equal(candidates.Take(1).Select(c => c.User.Id), result.UsersByRole[RoleType.TopOnePercent].Select(u => u.Id));
    }

    [Fact]
    public void BuildActivityRoleAssignments_ClampsNonEmptySmallGuildsToAtLeastOneUser()
    {
        List<ActivityRoleCandidate> candidates = CreateCandidates(5);

        ActivityRoleAssignmentResult result = ActivityService.BuildActivityRoleAssignments(candidates);

        Assert.Single(result.UsersByRole[RoleType.TopOnePercent]);
        Assert.Single(result.UsersByRole[RoleType.TopFivePercent]);
        Assert.Single(result.UsersByRole[RoleType.TopTenPercent]);
        Assert.Single(result.UsersByRole[RoleType.TopTwentyPercent]);
        Assert.Equal(2, result.UsersByRole[RoleType.TopThirtyPercent].Count);
    }

    [Fact]
    public void BuildActivityRoleAssignments_LeavesEmptyGuildsEmpty()
    {
        ActivityRoleAssignmentResult result = ActivityService.BuildActivityRoleAssignments([]);

        Assert.Empty(result.Candidates);
        Assert.All(ActivityService.ActivityRoleDefinitions, definition =>
            Assert.Empty(result.UsersByRole[definition.RoleType]));
    }

    private static List<ActivityRoleCandidate> CreateCandidates(int count)
    {
        return [.. Enumerable.Range(1, count).Select(index => new ActivityRoleCandidate
        {
            User = new User
            {
                Id = index,
                DiscordId = (ulong)index,
                Username = $"user-{index}"
            },
            TotalXp = count - index,
            MessageCount = count - index,
            LastActivity = DateTime.UtcNow.AddMinutes(-index)
        })];
    }
}
