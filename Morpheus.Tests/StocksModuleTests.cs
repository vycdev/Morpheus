using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Discord;
using Discord.Commands;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Modules;
using Morpheus.Services;

namespace Morpheus.Tests;

public class StocksModuleTests
{
    [SupportedCultureTheory("tr-TR")]
    [InlineData("GUILD")]
    public async Task ResolveTarget_AcceptsGuildKeywordUnderTurkishCulture(string target)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var module = new StocksModule(null!, null!, null!, null!);
            SocketCommandContextExtended context =
                (SocketCommandContextExtended)RuntimeHelpers.GetUninitializedObject(
                    typeof(SocketCommandContextExtended));
            context.DbGuild = new Guild { Id = 42, DiscordId = 1, Name = "Test guild" };
            ((IModuleBase)module).SetContext(context);
            var result = await module.ResolveTarget(target);

            Assert.NotNull(result);
            Assert.Equal(StockEntityType.Guild, result.Value.type);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void TryParseShareAmount_UsesInvariantDecimalSeparator()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo commaDecimalCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        commaDecimalCulture.NumberFormat.NumberDecimalSeparator = ",";
        commaDecimalCulture.NumberFormat.NumberGroupSeparator = ".";

        try
        {
            CultureInfo.CurrentCulture = commaDecimalCulture;

            bool parsed = StocksModule.TryParseShareAmount("12.50", out decimal amount);

            Assert.True(parsed);
            Assert.Equal(12.50m, amount);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("12,50")]
    [InlineData("1,000")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void TryParseShareAmount_RejectsAmbiguousOrNonPositiveValues(string value)
    {
        Assert.False(StocksModule.TryParseShareAmount(value, out _));
    }

    [Fact]
    public async Task StockPortfolioAsync_BoundsLargePortfolioDescription()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        User owner = new() { DiscordId = 1, Username = "owner", Balance = 1000m };
        List<User> stockUsers = [.. Enumerable.Range(1, 50).Select(index => new User
        {
            DiscordId = (ulong)(index + 1),
            Username = $"stock-user-{index:D2}-{new string('x', 18)}"
        })];
        db.Users.Add(owner);
        db.Users.AddRange(stockUsers);
        await db.SaveChangesAsync();

        List<Stock> stocks = [.. stockUsers.Select(user => new Stock
        {
            EntityType = StockEntityType.User,
            EntityId = user.Id,
            Price = 100m,
            PreviousPrice = 100m
        })];
        db.Stocks.AddRange(stocks);
        await db.SaveChangesAsync();

        db.StockHoldings.AddRange(stocks.Select(stock => new StockHolding
        {
            UserId = owner.Id,
            StockId = stock.Id,
            Shares = 1m,
            TotalInvested = 100m
        }));
        await db.SaveChangesAsync();

        LogsService logs = new(new LogQueue());
        StocksService stocksService = new(db, logs, new EconomyService(db, logs));
        TestStocksModule module = new(db, stocksService);

        await module.StockPortfolioAsync(CreateUser(owner.DiscordId, owner.Username));

        Embed embed = Assert.IsType<Embed>(module.LastEmbed);
        Assert.NotNull(embed.Description);
        Assert.InRange(embed.Description.Length, 1, EmbedBuilder.MaxDescriptionLength);
        Assert.Contains("more holdings", embed.Description);
        Assert.Contains("Total Holdings Value", embed.Description);
        Assert.Contains("$5000.00", embed.Description);
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

    private sealed class TestStocksModule(DB db, StocksService stocksService)
        : StocksModule(db, stocksService, null!, null!)
    {
        public Embed? LastEmbed { get; private set; }

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
            LastEmbed = embed;
            return Task.FromResult<IUserMessage>(null!);
        }
    }
}
