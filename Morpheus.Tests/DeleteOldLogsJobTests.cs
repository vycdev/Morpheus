using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Jobs;
using Morpheus.Services;
using Quartz;
using System.Reflection;

namespace Morpheus.Tests;

public class DeleteOldLogsJobTests
{
    [Fact]
    public async Task Execute_DeletesOnlyLogsOlderThanThirtyDays()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        DateTime now = DateTime.UtcNow;
        db.Logs.AddRange(
            new Log { Message = "expired", InsertDate = now.AddDays(-31) },
            new Log { Message = "recent", InsertDate = now.AddDays(-29) },
            new Log { Message = "current", InsertDate = now });
        await db.SaveChangesAsync();

        DeleteOldLogsJob job = new(new LogsService(new LogQueue()), db);
        await job.Execute(CreateContext(CancellationToken.None));

        db.ChangeTracker.Clear();
        string[] remaining = await db.Logs.OrderBy(log => log.InsertDate)
            .Select(log => log.Message).ToArrayAsync();
        Assert.Equal(["recent", "current"], remaining);
    }

    [Fact]
    public async Task Execute_WhenCanceledBeforeDeletion_PreservesOldLogs()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        db.Logs.Add(new Log
        {
            Message = "old log",
            InsertDate = DateTime.UtcNow.AddDays(-31)
        });
        await db.SaveChangesAsync();

        DeleteOldLogsJob job = new(new LogsService(new LogQueue()), db);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        IJobExecutionContext context = CreateContext(cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.Execute(context));
        db.ChangeTracker.Clear();
        Assert.Single(await db.Logs.ToListAsync());
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
            if (targetMethod?.ReturnType == typeof(CancellationToken))
                return CurrentCancellationToken;

            Type returnType = targetMethod?.ReturnType ?? typeof(void);
            return returnType == typeof(void) || !returnType.IsValueType
                ? null
                : Activator.CreateInstance(returnType);
        }
    }
}
