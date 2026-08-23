using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Jobs;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Tests;

public class StockUpdateJobTests
{
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
