using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class ChannelServiceConcurrencyTests
{
    [Fact]
    public async Task TryGetCreateChannel_WhenAnotherHandlerCreatesChannel_ReturnsPersistedChannel()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using (DB setup = new(options))
            await setup.Database.EnsureCreatedAsync();

        await using RacingDb db = new(options);
        db.InsertCompetingChannelOnNextSave = true;
        ChannelService service = new(db, new LogsService(new LogQueue()));

        Channel result = await service.TryGetCreateChannel(123, "current-name");

        Assert.Equal((ulong)123, result.DiscordId);
        Assert.Equal("current-name", result.Name);
        Assert.Equal(1, await db.Channels.CountAsync());
    }

    private sealed class RacingDb(DbContextOptions<DB> options) : DB(options)
    {
        public bool InsertCompetingChannelOnNextSave { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (InsertCompetingChannelOnNextSave)
            {
                InsertCompetingChannelOnNextSave = false;
                ChangeTracker.Clear();
                Channels.Add(new Channel
                {
                    DiscordId = 123,
                    Name = "stale-name"
                });
                await base.SaveChangesAsync(cancellationToken);
                throw new DbUpdateException("Simulated concurrent unique-key conflict.");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
