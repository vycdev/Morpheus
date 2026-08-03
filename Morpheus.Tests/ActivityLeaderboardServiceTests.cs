using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class ActivityLeaderboardServiceTests
{
    [Fact]
    public void ValidatePage_ReturnsEmptyMessageWhenNoUsersExist()
    {
        ActivityLeaderboardQueryResult? result = ActivityLeaderboardService.ValidatePage(
            page: 1,
            totalUsers: 0,
            emptyMessage: "No data.");

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("No data.", result.ErrorMessage);
    }

    [Fact]
    public void ValidatePage_ReturnsInvalidPageMessageWhenPageIsOutOfRange()
    {
        ActivityLeaderboardQueryResult? result = ActivityLeaderboardService.ValidatePage(
            page: 3,
            totalUsers: 11,
            emptyMessage: "No data.");

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Invalid page number. Please choose a page between 1 and 2.", result.ErrorMessage);
    }

    [Fact]
    public void CreatePage_ComputesTotalPagesFromConfiguredPageSize()
    {
        ActivityLeaderboardQueryResult result = ActivityLeaderboardService.CreatePage(
            "Title",
            ["line"],
            page: 2,
            totalUsers: 11,
            rankLine: "Your rank: #4");

        Assert.True(result.Success);
        Assert.NotNull(result.Page);
        Assert.Equal(2, result.Page.CurrentPage);
        Assert.Equal(2, result.Page.TotalPages);
        Assert.Equal("Your rank: #4", result.Page.RankLine);
    }

    [Fact]
    public void FormatLeaderboardMessage_UsesExistingCodeBlockShape()
    {
        ActivityLeaderboardPage page = new(
            "**Leaderboard**",
            ["[1] | user: Level 2 with 2000 XP"],
            CurrentPage: 1,
            TotalPages: 3,
            RankLine: "Your rank: #1");

        string message = ActivityLeaderboardService.FormatLeaderboardMessage(page);

        Assert.Equal(
            """
            **Leaderboard**
            ```js
            [1] | user: Level 2 with 2000 XP

            (Page 1/3)
            ```
            Your rank: #1

            """,
            message.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task GetGlobalMessageLeaderboardAsync_ExcludesUsersWithoutMessages()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        User activeUser = new() { DiscordId = 1, Username = "active" };
        User inactiveUser = new() { DiscordId = 2, Username = "inactive" };
        Guild guild = new() { DiscordId = 1, Name = "Test guild" };
        db.AddRange(activeUser, inactiveUser, guild);
        await db.SaveChangesAsync();

        db.UserLevels.AddRange(
            new UserLevels { UserId = activeUser.Id, GuildId = guild.Id, UserMessageCount = 5 },
            new UserLevels { UserId = inactiveUser.Id, GuildId = guild.Id, UserMessageCount = 0 });
        await db.SaveChangesAsync();

        ActivityLeaderboardService service = new(db);
        ActivityLeaderboardQueryResult result = await service.GetGlobalMessageLeaderboardAsync(null, page: 1);

        Assert.True(result.Success);
        Assert.NotNull(result.Page);
        Assert.Equal(["[1] | active: Messages 5"], result.Page.Lines);
        Assert.Equal(1, result.Page.TotalPages);
    }

    [Fact]
    public async Task GetGuildXpLeaderboardAsync_OrdersTiesByUserIdAcrossPages()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        Guild guild = new() { DiscordId = 1, Name = "Test guild" };
        List<User> users = [.. Enumerable.Range(1, 12)
            .Select(index => new User { DiscordId = (ulong)index, Username = $"user{index:00}" })];
        db.Add(guild);
        db.AddRange(users);
        await db.SaveChangesAsync();

        db.UserLevels.AddRange(users.AsEnumerable().Reverse().Select(user => new UserLevels
        {
            UserId = user.Id,
            GuildId = guild.Id,
            TotalXp = 100
        }));
        await db.SaveChangesAsync();

        ActivityLeaderboardService service = new(db);
        ActivityLeaderboardQueryResult firstPage = await service.GetGuildXpLeaderboardAsync(
            guild.Id,
            guild.Name,
            viewerUserId: null,
            page: 1);
        ActivityLeaderboardQueryResult secondPage = await service.GetGuildXpLeaderboardAsync(
            guild.Id,
            guild.Name,
            viewerUserId: null,
            page: 2);

        Assert.True(firstPage.Success);
        Assert.NotNull(firstPage.Page);
        Assert.Equal(
            users.Take(ActivityLeaderboardService.PageSize).Select((user, index) =>
                $"[{index + 1}] | {user.Username}: Level 0 with 100 XP"),
            firstPage.Page.Lines);

        Assert.True(secondPage.Success);
        Assert.NotNull(secondPage.Page);
        Assert.Equal(
            users.Skip(ActivityLeaderboardService.PageSize).Select((user, index) =>
                $"[{ActivityLeaderboardService.PageSize + index + 1}] | {user.Username}: Level 0 with 100 XP"),
            secondPage.Page.Lines);
    }
}
