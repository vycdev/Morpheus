using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Morpheus.Database;
using Morpheus.Database.Models;
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
