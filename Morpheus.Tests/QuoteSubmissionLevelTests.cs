using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class QuoteSubmissionLevelTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(4)]
    public async Task CreateAddRequestAsync_BelowDefaultLevel_DoesNotPersistRequests(int? level)
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        if (level.HasValue)
            await fixture.AddLevelAsync(fixture.Guild, level.Value);

        QuoteAddRequestResult result = await fixture.SubmitAsync();

        Assert.Equal(QuoteAddRequestStatus.LevelTooLow, result.Status);
        Assert.Equal(5, result.RequiredLevel);
        Assert.False(result.RequiresApprovalMessage);
        Assert.Empty(await fixture.Db.Quotes.ToListAsync());
        Assert.Empty(await fixture.Db.QuoteApprovalMessages.ToListAsync());
    }

    [Theory]
    [InlineData(5, 5, false)]
    [InlineData(5, 6, false)]
    [InlineData(8, 7, true)]
    [InlineData(8, 8, false)]
    [InlineData(0, 0, false)]
    public async Task CreateAddRequestAsync_UsesPersistedGuildThreshold(int minimumLevel, int level, bool denied)
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        fixture.Guild.QuoteSubmissionMinLevel = minimumLevel;
        await fixture.Db.SaveChangesAsync();
        await fixture.AddLevelAsync(fixture.Guild, level);

        QuoteAddRequestResult result = await fixture.SubmitAsync();

        Assert.Equal(denied ? QuoteAddRequestStatus.LevelTooLow : QuoteAddRequestStatus.PendingApproval, result.Status);
        Assert.Equal(denied ? 0 : 1, await fixture.Db.Quotes.CountAsync());
        Assert.Equal(denied ? 0 : 1, await fixture.Db.QuoteApprovalMessages.CountAsync());
    }

    [Fact]
    public async Task CreateAddRequestAsync_LevelInAnotherGuild_DoesNotGrantAccess()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Guild otherGuild = new() { DiscordId = 3, Name = "Other guild" };
        fixture.Db.Guilds.Add(otherGuild);
        await fixture.Db.SaveChangesAsync();
        await fixture.AddLevelAsync(otherGuild, 100);

        QuoteAddRequestResult result = await fixture.SubmitAsync();

        Assert.Equal(QuoteAddRequestStatus.LevelTooLow, result.Status);
        Assert.Empty(await fixture.Db.Quotes.ToListAsync());
    }

    [Theory]
    [InlineData(false, QuoteAddRequestStatus.PendingApproval)]
    [InlineData(true, QuoteAddRequestStatus.Approved)]
    public async Task CreateAddRequestAsync_Administrator_PreservesApprovalFlow(bool force, QuoteAddRequestStatus expected)
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        fixture.Guild.QuoteSubmissionMinLevel = int.MaxValue;
        await fixture.Db.SaveChangesAsync();

        QuoteAddRequestResult result = await fixture.SubmitAsync(isAdmin: true, force: force);

        Assert.Equal(expected, result.Status);
        Assert.Equal(force, (await fixture.Db.Quotes.SingleAsync()).Approved);
        Assert.Equal(force ? 0 : 1, await fixture.Db.QuoteApprovalMessages.CountAsync());
    }

    [Fact]
    public async Task CreateAddRequestAsync_ZeroThreshold_AllowsUserWithoutLevelRecord()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        fixture.Guild.QuoteSubmissionMinLevel = 0;
        await fixture.Db.SaveChangesAsync();

        Assert.Equal(QuoteAddRequestStatus.PendingApproval, (await fixture.SubmitAsync()).Status);
        Assert.Empty(await fixture.Db.UserLevels.ToListAsync());
    }

    [Fact]
    public async Task Guild_ExplicitZeroThreshold_IsNotReplacedByDatabaseDefault()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        Guild guild = new() { DiscordId = 3, Name = "Open guild", QuoteSubmissionMinLevel = 0 };
        fixture.Db.Guilds.Add(guild);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        Assert.Equal(0, (await fixture.Db.Guilds.SingleAsync(g => g.DiscordId == 3)).QuoteSubmissionMinLevel);
    }

    [Fact]
    public async Task Migration_BackfillsExistingGuildsAndDefaultsNewRowsToFive()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using DB db = new(new DbContextOptionsBuilder<DB>().UseSqlite(connection).Options);
        IMigrationsAssembly migrations = db.GetService<IMigrationsAssembly>();
        var entry = Assert.Single(migrations.Migrations, item => item.Key.EndsWith("_QuoteSubmissionMinimumLevel", StringComparison.Ordinal));
        Migration migration = migrations.CreateMigration(entry.Value, db.Database.ProviderName!);
        AddColumnOperation operation = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("Guilds", operation.Table);
        Assert.Equal("QuoteSubmissionMinLevel", operation.Name);
        Assert.Equal(5, operation.DefaultValue);
        Assert.False(operation.IsNullable);
        DropColumnOperation rollback = Assert.IsType<DropColumnOperation>(Assert.Single(migration.DownOperations));
        Assert.Equal(operation.Name, rollback.Name);
        Assert.Equal(operation.Table, rollback.Table);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Guilds (Id INTEGER PRIMARY KEY); INSERT INTO Guilds (Id) VALUES (1);");
        foreach (MigrationCommand command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Guilds (Id) VALUES (2);");
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT QuoteSubmissionMinLevel FROM Guilds ORDER BY Id";
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(5, reader.GetInt32(0));
        Assert.True(await reader.ReadAsync());
        Assert.Equal(5, reader.GetInt32(0));
        Assert.False(await reader.ReadAsync());
    }

    private sealed class Fixture(SqliteConnection connection, DB db, Guild guild, User user) : IAsyncDisposable
    {
        public DB Db { get; } = db;
        public Guild Guild { get; } = guild;

        public static async Task<Fixture> CreateAsync()
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            DB db = new(new DbContextOptionsBuilder<DB>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            Guild guild = new() { DiscordId = 1, Name = "Test guild" };
            User user = new() { DiscordId = 2, Username = "member" };
            db.AddRange(guild, user);
            await db.SaveChangesAsync();
            return new Fixture(connection, db, guild, user);
        }

        public async Task AddLevelAsync(Guild guild, int level)
        {
            Db.UserLevels.Add(new UserLevels { GuildId = guild.Id, UserId = user.Id, Level = level });
            await Db.SaveChangesAsync();
        }

        public Task<QuoteAddRequestResult> SubmitAsync(bool isAdmin = false, bool force = false) =>
            new QuoteService(Db).CreateAddRequestAsync(Guild.Id, user.Id, "quote", isAdmin, force, 123, 5);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
