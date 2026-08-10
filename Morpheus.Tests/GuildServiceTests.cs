using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class GuildServiceTests
{
    [Fact]
    public async Task TryGetCreateGuild_UpdatesStoredNameForExistingGuild()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        Guild guild = new()
        {
            DiscordId = 123,
            Name = "old-name"
        };
        await db.Guilds.AddAsync(guild);
        await db.SaveChangesAsync();

        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        GuildPrefixService prefixService = new(services.GetRequiredService<IServiceScopeFactory>());
        GuildService service = new(db, new LogsService(new LogQueue()), prefixService);

        Guild result = await service.TryGetCreateGuild(123, "new-name");

        Assert.Equal("new-name", result.Name);

        db.ChangeTracker.Clear();
        Guild persisted = await db.Guilds.SingleAsync(g => g.DiscordId == 123);
        Assert.Equal("new-name", persisted.Name);
    }
}