using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class UsersServiceTests
{
    [Fact]
    public async Task TryGetCreateUserAsync_WhenAnotherHandlerCreatesUser_ReturnsPersistedUser()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using (DB setup = new(options))
            await setup.Database.EnsureCreatedAsync();

        await using RacingDb db = new(options);
        db.InsertCompetingUserOnNextSave = true;
        UsersService service = new(db, new LogsService(new LogQueue()));

        User result = await service.TryGetCreateUserAsync(123, "first");

        Assert.Equal((ulong)123, result.DiscordId);
        Assert.Equal("concurrent", result.Username);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    private sealed class RacingDb : DB
    {
        public RacingDb(DbContextOptions<DB> options) : base(options)
        {
        }

        public bool InsertCompetingUserOnNextSave { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (InsertCompetingUserOnNextSave)
            {
                InsertCompetingUserOnNextSave = false;
                ChangeTracker.Clear();
                Users.Add(new User
                {
                    DiscordId = 123,
                    Username = "concurrent"
                });
                await base.SaveChangesAsync(cancellationToken);
                throw new DbUpdateException("Simulated concurrent unique-key conflict.");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}