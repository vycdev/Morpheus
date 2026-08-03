using Discord.WebSocket;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Jobs;
using Morpheus.Services;

namespace Morpheus.Tests;

public class TemporaryBansJobTests
{
    [Fact]
    public async Task Execute_WhenGuildIsUnavailable_LeavesBanPendingForRetry()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        TemporaryBan ban = new()
        {
            GuildId = 1,
            UserId = 2,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        db.TemporaryBans.Add(ban);
        await db.SaveChangesAsync();

        using DiscordSocketClient discordClient = new();
        TemporaryBansJob job = new(new LogsService(new LogQueue()), db, discordClient);

        await job.Execute(null!);
        db.ChangeTracker.Clear();

        TemporaryBan persistedBan = await db.TemporaryBans.SingleAsync();
        Assert.Null(persistedBan.UnbannedAt);
    }
}
