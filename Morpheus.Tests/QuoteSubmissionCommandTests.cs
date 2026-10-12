using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.MCP;
using Morpheus.Modules;
using Morpheus.Services;

namespace Morpheus.Tests;

public class QuoteSubmissionCommandTests
{
    [Fact]
    public async Task AddQuote_BelowMinimum_ReportsRequiredLevel()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        TestQuotesModule module = fixture.CreateQuotesModule(isAdmin: false);

        await module.AddQuote("a quote");

        Assert.Equal("You must reach level 5 in this server to submit quotes. Administrators bypass this requirement.", module.LastReply);
        Assert.Empty(await fixture.Db.Quotes.ToListAsync());
        Assert.Empty(await fixture.Db.QuoteApprovalMessages.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddQuote_AdministratorWithoutLevel_BypassesRequirement(bool force)
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        TestQuotesModule module = fixture.CreateQuotesModule(isAdmin: true);

        await module.AddQuote(force ? "force a quote" : "a quote");

        Quote quote = Assert.Single(await fixture.Db.Quotes.ToListAsync());
        Assert.True(quote.Approved);
        Assert.Equal("a quote", quote.Content);
        Assert.Empty(await fixture.Db.UserLevels.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    public async Task SetQuoteMinLevel_PersistsValidLevelsOnly(int level)
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        TestGuildModule module = new(fixture.Db);
        ((IModuleBase)module).SetContext(fixture.CreateContext(false));
        fixture.Db.Guilds.Add(new Guild { DiscordId = 3, Name = "Other guild", QuoteSubmissionMinLevel = 12 });
        await fixture.Db.SaveChangesAsync();
        await module.SetQuoteMinLevel(level);

        fixture.Db.ChangeTracker.Clear();
        Guild guild = await fixture.Db.Guilds.SingleAsync(g => g.DiscordId == 1);
        Assert.Equal(12, (await fixture.Db.Guilds.SingleAsync(g => g.DiscordId == 3)).QuoteSubmissionMinLevel);
        Assert.Equal(level < 0 ? 5 : level, guild.QuoteSubmissionMinLevel);
        Assert.Equal(level < 0 ? "Minimum quote submission level must be at least 0." : $"Minimum quote submission level set to {level}. Administrators bypass this requirement.", module.LastReply);
    }

    [Fact]
    public void SetQuoteMinLevel_RequiresGuildAdministrator()
    {
        var method = typeof(GuildModule).GetMethod(nameof(GuildModule.SetQuoteMinLevel))!;
        var permissions = Assert.Single(method.GetCustomAttributes(typeof(RequireUserPermissionAttribute), false));
        Assert.Equal(GuildPermission.Administrator, ((RequireUserPermissionAttribute)permissions).GuildPermission);
        var context = Assert.Single(method.GetCustomAttributes(typeof(RequireContextAttribute), false));
        Assert.Equal(ContextType.Guild, ((RequireContextAttribute)context).Contexts);
    }

    [Fact]
    public async Task AddQuote_UsesChangedSettingDespiteDetachedContext()
    {
        await using Fixture fixture = await Fixture.CreateAsync();
        TestGuildModule settings = new(fixture.Db);
        SocketCommandContextExtended context = fixture.CreateContext(false);
        ((IModuleBase)settings).SetContext(context);
        await settings.SetQuoteMinLevel(8);
        TestQuotesModule quotes = fixture.CreateQuotesModule(false);
        context.DbGuild = new Guild { Id = context.DbGuild!.Id, QuoteSubmissionMinLevel = 5 };
        ((IModuleBase)quotes).SetContext(context);

        await quotes.AddQuote("a quote");

        Assert.Equal("You must reach level 8 in this server to submit quotes. Administrators bypass this requirement.", quotes.LastReply);
        Assert.Empty(await fixture.Db.Quotes.ToListAsync());
    }

    private sealed class Fixture(SqliteConnection connection, DB db, Guild guild) : IAsyncDisposable
    {
        public DB Db { get; } = db;
        private readonly DiscordSocketClient client = new();

        public static async Task<Fixture> CreateAsync()
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            DB db = new(new DbContextOptionsBuilder<DB>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            Guild guild = new() { DiscordId = 1, Name = "Guild" };
            db.Guilds.Add(guild);
            await db.SaveChangesAsync();
            return new Fixture(connection, db, guild);
        }

        public SocketCommandContextExtended CreateContext(bool isAdmin)
        {
            IGuildUser user = DiscordProxy.Create<IGuildUser>((method, _) => method.Name switch
            {
                "get_Id" => 2UL,
                "get_Username" => "member",
                "get_GuildPermissions" => new GuildPermissions(isAdmin ? (ulong)GuildPermission.Administrator : 0),
                _ => DiscordProxy.DefaultValue(method.ReturnType)
            });
            IMessageChannel channel = DiscordProxy.Create<IMessageChannel>((method, _) => DiscordProxy.DefaultValue(method.ReturnType));
            IUserMessage message = DiscordProxy.Create<IUserMessage>((method, _) => method.Name switch
            {
                "get_Author" => user,
                "get_Channel" => channel,
                _ => DiscordProxy.DefaultValue(method.ReturnType)
            });
            return new SocketCommandContextExtended(client, message, guild, null);
        }

        public TestQuotesModule CreateQuotesModule(bool isAdmin)
        {
            LogsService logs = new(new LogQueue());
            TestQuotesModule module = new(new UsersService(Db, logs), logs, new InteractionsHandler(client), new QuoteService(Db));
            ((IModuleBase)module).SetContext(CreateContext(isAdmin));
            return module;
        }

        public async ValueTask DisposeAsync()
        {
            await client.DisposeAsync();
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class TestQuotesModule(UsersService users, LogsService logs, InteractionsHandler interactions, QuoteService quotes)
        : QuotesModule(users, logs, interactions, quotes)
    {
        public string? LastReply { get; private set; }

        protected override Task<IUserMessage> ReplyAsync(string? message = null, bool isTTS = false, Embed? embed = null,
            RequestOptions? options = null, AllowedMentions? allowedMentions = null, MessageReference? messageReference = null,
            MessageComponent? components = null, ISticker[]? stickers = null, Embed[]? embeds = null, MessageFlags flags = MessageFlags.None)
        {
            LastReply = message;
            return Task.FromResult<IUserMessage>(null!);
        }
    }

    private sealed class TestGuildModule(DB db) : GuildModule(null!, null!, null!, null!, db, null!)
    {
        public string? LastReply { get; private set; }

        protected override Task<IUserMessage> ReplyAsync(string? message = null, bool isTTS = false, Embed? embed = null,
            RequestOptions? options = null, AllowedMentions? allowedMentions = null, MessageReference? messageReference = null,
            MessageComponent? components = null, ISticker[]? stickers = null, Embed[]? embeds = null, MessageFlags flags = MessageFlags.None)
        {
            LastReply = message;
            return Task.FromResult<IUserMessage>(null!);
        }
    }
}
