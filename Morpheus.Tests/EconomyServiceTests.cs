using System.Data.Common;
using System.Globalization;
using Discord;
using Discord.Commands;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.MCP;
using Morpheus.Modules;
using Morpheus.Services;

namespace Morpheus.Tests;

public class EconomyServiceTests
{
    private static CultureInfo CreateCommaDecimalCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = ".";
        return culture;
    }

    [Fact]
    public void OrderUserIdsForUpdate_DeduplicatesAndSortsIds()
    {
        int[] orderedIds = [.. EconomyService.OrderUserIdsForUpdate([42, 7, 42, 1])];

        Assert.Equal([1, 7, 42], orderedIds);
    }

    [Fact]
    public void FormatMoneyForStorage_UsesInvariantTwoDecimalPlaces()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CreateCommaDecimalCulture();

            string formatted = EconomyService.FormatMoneyForStorage(1234.5m);

            Assert.Equal("1234.50", formatted);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ParseMoneyFromStorage_PrefersInvariantCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CreateCommaDecimalCulture();

            decimal parsed = EconomyService.ParseMoneyFromStorage("1,234.50", fallback: 99m);

            Assert.Equal(1234.50m, parsed);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ParseMoneyFromStorage_FallsBackToCurrentCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CreateCommaDecimalCulture();

            decimal parsed = EconomyService.ParseMoneyFromStorage("1234,50", fallback: 99m);

            Assert.Equal(1234.50m, parsed);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ParseMoneyFromStorage_ReturnsFallbackForInvalidValues()
    {
        decimal parsed = EconomyService.ParseMoneyFromStorage("not-money", fallback: 123m);

        Assert.Equal(123m, parsed);
    }

    [Theory]
    [InlineData("en-US", "ubi donate 1.25000000000000000000000000001", CommandError.UnmetPrecondition)]
    [InlineData("ro-RO", "donate 1,25000000000000000000000000001", CommandError.UnmetPrecondition)]
    [InlineData("en-US", "ubi donate 0.004", CommandError.UnmetPrecondition)]
    [InlineData("en-US", "ubi donate invalid", CommandError.ParseFailed)]
    [InlineData("en-US", "ubi donate +1,234.25000000000000000000000000000", null)]
    [InlineData("ro-RO", "donate 1.234,2500+", null)]
    [InlineData("en-US", "donate \"1 25000000000000000000000000001\"", CommandError.UnmetPrecondition, "\u00A0")]
    [InlineData("en-US", "donate \"1234 2500+ \"", null, "\u202F")]
    [InlineData("en-US", "ubi donate \" 123\u00A0\"", null, "\u00A0", "123")]
    [InlineData("en-US", "ubi donate \"1 .25000000000000000000000000001\"", CommandError.UnmetPrecondition, "\u00A0.")]
    public async Task DonateCommand_RequiresWholeCentAmounts(
        string cultureName, string input, CommandError? expectedError, string? decimalSeparator = null,
        string expectedAmount = "1234.25")
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo culture = new(cultureName);
            if (decimalSeparator != null)
                culture.NumberFormat.NumberDecimalSeparator = decimalSeparator;
            CultureInfo.CurrentCulture = culture;
            using ServiceProvider services = new ServiceCollection()
                .AddDbContext<DB>(options => options.UseSqlite("Data Source=:memory:"))
                .AddSingleton<LogQueue>()
                .AddSingleton<LogsService>()
                .AddTransient<EconomyService>()
                .AddTransient<UsersService>()
                .BuildServiceProvider();
            CommandService commands = new();
            await commands.AddModuleAsync<EconomyModule>(services);

            if (expectedError == CommandError.UnmetPrecondition)
            {
                IUser user = DiscordProxy.Create<IUser>((method, _) => method.Name == "get_Id" ? 1UL : null);
                ICommandContext context = DiscordProxy.Create<ICommandContext>((method, _) => method.Name == "get_User" ? user : null);

                IResult execution = await commands.ExecuteAsync(context, input, services);

                Assert.Equal(expectedError, execution.Error);
                Assert.Equal(EconomyService.WholeCentDonationMessage, execution.ErrorReason);
                return;
            }

            SearchResult search = commands.Search(input);
            CommandMatch match = Assert.Single(search.Commands, candidate => candidate.Command.Name == "Donate to UBI");

            ParseResult result = await match.Command.ParseAsync(null!, match.Alias.Length, search, services: services);

            if (expectedError == CommandError.ParseFailed)
            {
                Assert.Equal(expectedError, result.Error);
                return;
            }

            Assert.True(result.IsSuccess, result.ErrorReason);
            TypeReaderResult argument = Assert.Single(result.ArgValues);
            EconomyModule.DonationAmount donation = Assert.IsType<EconomyModule.DonationAmount>(Assert.Single(argument.Values).Value);
            ParameterInfo parameter = Assert.Single(match.Command.Parameters);
            PreconditionResult precondition = await parameter.CheckPreconditionsAsync(null!, donation, services);

            Assert.True(precondition.IsSuccess, precondition.ErrorReason);
            Assert.Equal(decimal.Parse(expectedAmount, CultureInfo.InvariantCulture), donation.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task DonateToUbi_RejectsAmountsFinerThanTheStoredPoolPrecision()
    {
        await using SqliteConnection connection = await CreatePostgresCompatibleConnectionAsync();
        await using DB db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        User user = new() { DiscordId = 1, Username = "donor", Balance = 10m };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        EconomyService economy = new(db, new LogsService(new LogQueue()));

        var (success, _) = await economy.DonateToUbi(user.Id, 0.004m);
        Assert.False(success);

        (success, _) = await economy.DonateToUbi(user.Id, 1.255m);
        Assert.False(success);

        db.ChangeTracker.Clear();
        Assert.Equal(10m, (await db.Users.SingleAsync()).Balance);
        Assert.False(await db.BotSettings.AnyAsync(setting => setting.Key == "ubi_pool"));
        Assert.Empty(await db.StockTransactions.ToListAsync());

        (success, _) = await economy.DonateToUbi(user.Id, 1.25m);

        Assert.True(success);
        db.ChangeTracker.Clear();
        Assert.Equal(8.75m, (await db.Users.SingleAsync()).Balance);
        Assert.Equal("1.25", (await db.BotSettings.SingleAsync(setting => setting.Key == "ubi_pool")).Value);
    }

    private static async Task<SqliteConnection> CreatePostgresCompatibleConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateFunction("hashtext", (string key) => key.Length);
        connection.CreateFunction("pg_advisory_xact_lock", (long _) => 0L);
        return connection;
    }

    private static DB CreateDb(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .AddInterceptors(new StripRowLocksInterceptor())
            .Options);

    // SQLite has no row locks; drop the clause so the Postgres locking queries still run.
    private sealed class StripRowLocksInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            command.CommandText = command.CommandText.Replace("FOR UPDATE", string.Empty, StringComparison.Ordinal);
            return ValueTask.FromResult(result);
        }
    }
}
