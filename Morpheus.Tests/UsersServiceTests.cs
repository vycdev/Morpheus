using System.Reflection;
using Discord;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class UsersServiceTests
{
    [Fact]
    public async Task TryGetCreateUser_CreatesUserFromNonSocketUser()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        UsersService service = new(db, new LogsService(new LogQueue()));
        IUser discordUser = CreateUser(123, "rest-user");

        User result = await service.TryGetCreateUser(discordUser);

        Assert.Equal((ulong)123, result.DiscordId);
        Assert.Equal("rest-user", result.Username);
        Assert.Single(await db.Users.ToListAsync());
    }

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

    private static IUser CreateUser(ulong id, string username)
    {
        IUser user = DispatchProxy.Create<IUser, UserProxy>();
        UserProxy proxy = (UserProxy)(object)user;
        proxy.Id = id;
        proxy.Username = username;
        return user;
    }

    public class UserProxy : DispatchProxy
    {
        public ulong Id { get; set; }
        public string Username { get; set; } = string.Empty;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            "get_Id" => Id,
            "get_Username" => Username,
            _ => throw new NotSupportedException(targetMethod?.Name)
        };
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
