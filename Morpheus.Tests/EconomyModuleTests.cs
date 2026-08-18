using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Extensions;
using Morpheus.Modules;
using Morpheus.Services;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Morpheus.Tests;

public class EconomyModuleTests
{
    [Fact]
    public async Task Rob_AcceptsNonSocketUserTarget()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        LogsService logs = new(new LogQueue());
        UsersService users = new(db, logs);
        TestEconomyModule module = new(new EconomyService(db, logs), users);
        SocketUser robber = CreateSocketUser(1, "robber");
        SocketCommandContextExtended context =
            (SocketCommandContextExtended)RuntimeHelpers.GetUninitializedObject(
                typeof(SocketCommandContextExtended));
        typeof(SocketCommandContext)
            .GetField("<User>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(context, robber);
        ((IModuleBase)module).SetContext(context);

        IUser victim = CreateUser(2, "rest-victim");

        await module.Rob(victim);

        Assert.Equal(
            "rest-victim",
            (await db.Users.SingleAsync(user => user.DiscordId == victim.Id)).Username);
        Assert.NotNull(module.LastReply);
    }

    private static SocketUser CreateSocketUser(ulong id, string username)
    {
        Type userType = typeof(SocketUser).Assembly.GetType("Discord.WebSocket.SocketGlobalUser")!;
        ConstructorInfo constructor = userType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(DiscordSocketClient), typeof(ulong)],
            modifiers: null)!;
        SocketUser user = (SocketUser)constructor.Invoke([new DiscordSocketClient(), id]);
        userType.GetProperty(nameof(IUser.Username))!.SetValue(user, username);
        userType.GetProperty(nameof(IUser.IsBot))!.SetValue(user, false);
        return user;
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

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name switch
            {
                "get_Id" => Id,
                "get_Username" => Username,
                "get_IsBot" => false,
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
    }

    private sealed class TestEconomyModule(
        EconomyService economyService,
        UsersService usersService) : EconomyModule(economyService, usersService)
    {
        public string? LastReply { get; private set; }

        protected override Task<IUserMessage> ReplyAsync(
            string? message = null,
            bool isTTS = false,
            Embed? embed = null,
            RequestOptions? options = null,
            AllowedMentions? allowedMentions = null,
            MessageReference? messageReference = null,
            MessageComponent? components = null,
            ISticker[]? stickers = null,
            Embed[]? embeds = null,
            MessageFlags flags = MessageFlags.None)
        {
            LastReply = message;
            return Task.FromResult<IUserMessage>(null!);
        }
    }
}
