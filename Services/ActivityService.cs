using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;

namespace Morpheus.Services;

public class ActivityService(DB dbContext)
{
    public static IReadOnlyList<ActivityRoleDefinition> ActivityRoleDefinitions { get; } =
    [
        new(RoleType.TopOnePercent, 0.01),
        new(RoleType.TopFivePercent, 0.05),
        new(RoleType.TopTenPercent, 0.10),
        new(RoleType.TopTwentyPercent, 0.20),
        new(RoleType.TopThirtyPercent, 0.30)
    ];

    public ActivityRoleAssignmentResult GetActivityRoleAssignments(int dbGuildId, IEnumerable<ulong> currentGuildMemberIds, int days = 30)
    {
        List<ActivityRoleCandidate> candidates = GetTopActivity(dbGuildId, currentGuildMemberIds, days);

        return BuildActivityRoleAssignments(candidates);
    }

    public List<ActivityRoleCandidate> GetTopActivity(int dbGuildId, IEnumerable<ulong>? currentGuildMemberIds = null, int days = 30)
    {
        DateTime date = DateTime.UtcNow.AddDays(-days);
        HashSet<ulong>? currentMemberIds = currentGuildMemberIds?.ToHashSet();

        var aggregates = dbContext.UserActivity
            .AsNoTracking()
            .Where(ua => ua.GuildId == dbGuildId && ua.InsertDate >= date)
            .GroupBy(ua => ua.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalXp = g.Sum(a => (long)a.XpGained),
                MessageCount = g.Count(),
                LastActivity = g.Max(a => a.InsertDate)
            })
            .Where(a => a.TotalXp > 0)
            .ToList();

        List<int> userIds = [.. aggregates.Select(a => a.UserId)];
        Dictionary<int, User> users = dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToList()
            .Where(u => currentMemberIds == null || currentMemberIds.Contains(u.DiscordId))
            .ToDictionary(u => u.Id);

        List<ActivityRoleCandidate> candidates = [.. aggregates
            .Where(a => users.ContainsKey(a.UserId))
            .Select(a => new ActivityRoleCandidate
            {
                User = users[a.UserId],
                TotalXp = a.TotalXp,
                MessageCount = a.MessageCount,
                LastActivity = a.LastActivity
            })
            .OrderByDescending(c => c.TotalXp)
            // Break ties by real activity signals before falling back to a stable ID order.
            .ThenByDescending(c => c.LastActivity)
            .ThenByDescending(c => c.MessageCount)
            .ThenBy(c => c.User.DiscordId)];

        return candidates;
    }

    public static ActivityRoleAssignmentResult BuildActivityRoleAssignments(List<ActivityRoleCandidate> candidates)
    {
        Dictionary<RoleType, List<User>> usersByRole = [];

        foreach (ActivityRoleDefinition definition in ActivityRoleDefinitions)
        {
            int userCount = GetCumulativeRoleUserCount(candidates.Count, definition.Percent);

            usersByRole[definition.RoleType] = candidates
                .Take(userCount)
                .Select(c => c.User)
                .ToList();
        }

        return new ActivityRoleAssignmentResult
        {
            Candidates = candidates,
            UsersByRole = usersByRole
        };
    }

    public static int GetCumulativeRoleUserCount(int totalUsers, double percent)
    {
        if (totalUsers <= 0)
            return 0;

        int count = (int)Math.Ceiling(totalUsers * percent);
        return Math.Clamp(count, 1, totalUsers);
    }
}

public sealed record ActivityRoleDefinition(RoleType RoleType, double Percent);

public class ActivityRoleCandidate
{
    public User User { get; set; } = null!;
    public long TotalXp { get; set; }
    public int MessageCount { get; set; }
    public DateTime LastActivity { get; set; }
}

public class ActivityRoleAssignmentResult
{
    public List<ActivityRoleCandidate> Candidates { get; set; } = [];
    public Dictionary<RoleType, List<User>> UsersByRole { get; set; } = [];
    public int EligibleUserCount => Candidates.Count;
}
