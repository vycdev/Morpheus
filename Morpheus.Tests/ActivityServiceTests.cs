using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class ActivityServiceTests
{
    [Fact]
    public async Task GetActivityRoleAssignments_RanksOnlyCurrentMembersWithRecentGuildActivity()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        Guild guild = new() { DiscordId = 1, Name = "Current guild" };
        Guild otherGuild = new() { DiscordId = 2, Name = "Other guild" };
        User first = new() { DiscordId = 10, Username = "first" };
        User second = new() { DiscordId = 20, Username = "second" };
        User departed = new() { DiscordId = 30, Username = "departed" };
        db.AddRange(guild, otherGuild, first, second, departed);
        await db.SaveChangesAsync();

        DateTime recent = DateTime.UtcNow.AddDays(-1);
        DateTime old = DateTime.UtcNow.AddDays(-31);
        db.UserActivity.AddRange(
            new UserActivity { UserId = first.Id, GuildId = guild.Id, XpGained = 8, InsertDate = recent },
            new UserActivity { UserId = first.Id, GuildId = guild.Id, XpGained = 100, InsertDate = old },
            new UserActivity { UserId = second.Id, GuildId = guild.Id, XpGained = 4, InsertDate = recent },
            new UserActivity { UserId = departed.Id, GuildId = guild.Id, XpGained = 200, InsertDate = recent },
            new UserActivity { UserId = second.Id, GuildId = otherGuild.Id, XpGained = 300, InsertDate = recent });
        await db.SaveChangesAsync();

        ActivityRoleAssignmentResult result = new ActivityService(db)
            .GetActivityRoleAssignments(guild.Id, [first.DiscordId, second.DiscordId]);

        Assert.Equal(2, result.EligibleUserCount);
        Assert.Equal([first.DiscordId, second.DiscordId], result.Candidates.Select(c => c.User.DiscordId));
        Assert.Equal([8L, 4L], result.Candidates.Select(c => c.TotalXp));
        Assert.Equal([first.DiscordId], result.UsersByRole[RoleType.TopOnePercent].Select(u => u.DiscordId));
    }

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
