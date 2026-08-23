using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class GuildServiceConcurrencyTests
{
    [Fact]
    public async Task TryGetCreateGuild_WhenAnotherHandlerCreatesGuild_ReturnsPersistedGuild()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using (DB setup = new(options))
            await setup.Database.EnsureCreatedAsync();

        await using RacingDb db = new(options);
        db.InsertCompetingGuildOnNextSave = true;
        GuildPrefixService prefixService = new(null!);
        GuildService service = new(db, new LogsService(new LogQueue()), prefixService);

        Guild result = await service.TryGetCreateGuild(123, "current-name");

        Assert.Equal((ulong)123, result.DiscordId);
        Assert.Equal("current-name", result.Name);
        Assert.Equal("persisted-prefix", await prefixService.GetPrefixAsync(123));
        Assert.Equal(1, await db.Guilds.CountAsync());

        db.ChangeTracker.Clear();
        Guild persistedGuild = await db.Guilds.SingleAsync();
        Assert.Equal("current-name", persistedGuild.Name);
    }

    private sealed class RacingDb(DbContextOptions<DB> options) : DB(options)
    {
        public bool InsertCompetingGuildOnNextSave { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (InsertCompetingGuildOnNextSave)
            {
                InsertCompetingGuildOnNextSave = false;
                ChangeTracker.Clear();
                Guilds.Add(new Guild
                {
                    DiscordId = 123,
                    Name = "stale-name",
                    Prefix = "persisted-prefix"
                });
                await base.SaveChangesAsync(cancellationToken);
                throw new DbUpdateException("Simulated concurrent unique-key conflict.");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
