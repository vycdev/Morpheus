using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Modules;

namespace Morpheus.Tests;

public class LevelsModuleTests
{
    [Fact]
    public async Task SumTotalXp_ReturnsLongTotalWhenGuildXpExceedsIntMaxValue()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        User user = new() { DiscordId = 1, Username = "user" };
        Guild firstGuild = new() { DiscordId = 1, Name = "First guild" };
        Guild secondGuild = new() { DiscordId = 2, Name = "Second guild" };
        db.AddRange(user, firstGuild, secondGuild);
        await db.SaveChangesAsync();

        db.UserLevels.AddRange(
            new UserLevels { UserId = user.Id, GuildId = firstGuild.Id, TotalXp = int.MaxValue },
            new UserLevels { UserId = user.Id, GuildId = secondGuild.Id, TotalXp = 1 });
        await db.SaveChangesAsync();

        IQueryable<UserLevels> userLevels = db.UserLevels.Where(level => level.UserId == user.Id);

        long totalXp = LevelsModule.SumTotalXp(userLevels);

        Assert.Equal((long)int.MaxValue + 1, totalXp);
    }
}
