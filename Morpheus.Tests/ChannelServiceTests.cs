using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class ChannelServiceTests
{
    [Fact]
    public async Task TryGetCreateChannel_UpdatesStoredNameForExistingChannel()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        Channel channel = new()
        {
            DiscordId = 123,
            Name = "old-name"
        };
        await db.Channels.AddAsync(channel);
        await db.SaveChangesAsync();

        ChannelService service = new(db, new LogsService(new LogQueue()));

        Channel result = await service.TryGetCreateChannel(123, "new-name");

        Assert.Equal("new-name", result.Name);

        db.ChangeTracker.Clear();
        Channel persisted = await db.Channels.SingleAsync(c => c.DiscordId == 123);
        Assert.Equal("new-name", persisted.Name);
    }
}