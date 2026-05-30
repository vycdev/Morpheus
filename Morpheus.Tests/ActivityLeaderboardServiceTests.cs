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
}
