using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;
using Morpheus.Jobs;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Tests;

public class StockUpdateJobTests
{
    [Fact]
    public async Task Execute_UpdatesDueStocksOnlyOncePerUtcDay()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        DateTime utcNow = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);
        DateTime todayStart = utcNow.Date;
        Stock due = new()
        {
            EntityType = StockEntityType.User,
            EntityId = 1001,
            UpdateTimeMinutes = 0,
            LastUpdatedDate = todayStart.AddDays(-1)
        };
        Stock alreadyUpdated = new()
        {
            EntityType = StockEntityType.User,
            EntityId = 1002,
            UpdateTimeMinutes = 0,
            LastUpdatedDate = todayStart
        };
        db.Stocks.AddRange(due, alreadyUpdated);
        await db.SaveChangesAsync();

        LogsService logsService = new(new LogQueue());
        StockUpdateJob job = new(logsService, db,
            new StocksService(db, logsService, new EconomyService(db, logsService)));
        IJobExecutionContext context = CreateContext(CancellationToken.None);

        await job.ExecuteAtAsync(context, utcNow);
        db.ChangeTracker.Clear();
        Stock updatedDue = await db.Stocks.SingleAsync(s => s.Id == due.Id);
        Stock untouched = await db.Stocks.SingleAsync(s => s.Id == alreadyUpdated.Id);
        Assert.Equal(utcNow, updatedDue.LastUpdatedDate);
        Assert.Equal(todayStart, untouched.LastUpdatedDate);

        DateTime firstUpdate = updatedDue.LastUpdatedDate;
        await job.ExecuteAtAsync(context, utcNow.AddMinutes(5));
        db.ChangeTracker.Clear();
        Assert.Equal(firstUpdate, (await db.Stocks.SingleAsync(s => s.Id == due.Id)).LastUpdatedDate);
    }

    [Fact]
    public async Task Execute_DoesNotUpdateStockUntilItsScheduledUtcMinute()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        DateTime scheduled = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);
        DateTime lastUpdated = scheduled.Date.AddDays(-1);
        Stock stock = new()
        {
            EntityType = StockEntityType.User,
            EntityId = 1001,
            UpdateTimeMinutes = 8 * 60,
            LastUpdatedDate = lastUpdated
        };
        db.Stocks.Add(stock);
        await db.SaveChangesAsync();

        LogsService logsService = new(new LogQueue());
        StockUpdateJob job = new(logsService, db,
            new StocksService(db, logsService, new EconomyService(db, logsService)));
        IJobExecutionContext context = CreateContext(CancellationToken.None);

        await job.ExecuteAtAsync(context, scheduled.AddSeconds(-1));
        db.ChangeTracker.Clear();
        Assert.Equal(lastUpdated, (await db.Stocks.SingleAsync(s => s.Id == stock.Id)).LastUpdatedDate);

        await job.ExecuteAtAsync(context, scheduled);
        db.ChangeTracker.Clear();
        Assert.Equal(scheduled, (await db.Stocks.SingleAsync(s => s.Id == stock.Id)).LastUpdatedDate);
    }

    [Fact]
    public async Task Execute_WhenSchedulerCancels_PropagatesCancellation()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        LogsService logsService = new(new LogQueue());
        EconomyService economyService = new(db, logsService);
        StocksService stocksService = new(db, logsService, economyService);
        StockUpdateJob job = new(logsService, db, stocksService);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        IJobExecutionContext context = CreateContext(cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.Execute(context));
    }

    private static IJobExecutionContext CreateContext(CancellationToken cancellationToken)
    {
        JobExecutionContextProxy.CurrentCancellationToken = cancellationToken;
        return DispatchProxy.Create<IJobExecutionContext, JobExecutionContextProxy>();
    }

    private class JobExecutionContextProxy : DispatchProxy
    {
        public static CancellationToken CurrentCancellationToken { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_CancellationToken")
                return CurrentCancellationToken;

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
