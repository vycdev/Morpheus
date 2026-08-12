using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Modules;

namespace Morpheus.Tests;

public class ButtonModuleTests
{
    [Theory]
    [InlineData(10L, 100L, false)]
    [InlineData(101L, 100L, true)]
    [InlineData(100L, 100L, false)]
    [InlineData(1L, null, false)]
    public void IsNewBestScore_ComparesAgainstHighestPreviousScore(
        long score,
        long? bestScore,
        bool expected)
    {
        Assert.Equal(expected, ButtonModule.IsNewBestScore(score, bestScore));
    }

    [Fact]
    public void OrderPressScores_UsesIdToBreakScoreTies()
    {
        ButtonGamePress[] presses =
        [
            new() { Id = 3, Score = 100 },
            new() { Id = 1, Score = 100 },
            new() { Id = 4, Score = 200 },
            new() { Id = 2, Score = 100 }
        ];

        int[] result =
        [.. ButtonModule.OrderPressScores(presses.AsQueryable()).Select(press => press.Id)];

        Assert.Equal([4, 1, 2, 3], result);
    }

    [Fact]
    public void OrderGuildScores_UsesGuildIdToBreakScoreTies()
    {
        ButtonGamePress[] presses =
        [
            new() { GuildId = 3, Score = 100 },
            new() { GuildId = 1, Score = 100 },
            new() { GuildId = 4, Score = 200 },
            new() { GuildId = 2, Score = 100 }
        ];

        int?[] result =
        [.. ButtonModule.OrderGuildScores(presses.AsQueryable()).Select(score => score.GuildId)];

        Assert.Equal([4, 1, 2, 3], result);
    }

    [Fact]
    public async Task OrderGuildScores_ExecutesGroupedDatabaseQuery()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        User user = new() { DiscordId = 1, Username = "button-user" };
        Guild[] guilds =
        [
            new() { DiscordId = 1, Name = "guild-1" },
            new() { DiscordId = 2, Name = "guild-2" },
            new() { DiscordId = 3, Name = "guild-3" },
            new() { DiscordId = 4, Name = "guild-4" }
        ];
        db.Add(user);
        db.AddRange(guilds);
        await db.SaveChangesAsync();

        db.ButtonGamePresses.AddRange(
            new() { UserId = user.Id, GuildId = guilds[2].Id, Score = 40 },
            new() { UserId = user.Id, GuildId = guilds[0].Id, Score = 100 },
            new() { UserId = user.Id, GuildId = guilds[3].Id, Score = 200 },
            new() { UserId = user.Id, GuildId = guilds[1].Id, Score = 100 },
            new() { UserId = user.Id, GuildId = guilds[2].Id, Score = 60 });
        await db.SaveChangesAsync();

        List<int?> result = await ButtonModule.OrderGuildScores(db.ButtonGamePresses)
            .Select(score => score.GuildId)
            .ToListAsync();

        Assert.Equal(
            [guilds[3].Id, guilds[0].Id, guilds[1].Id, guilds[2].Id],
            result);
    }
}
