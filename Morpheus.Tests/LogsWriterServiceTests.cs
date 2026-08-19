using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Database;
using Morpheus.Services;

namespace Morpheus.Tests;

public class LogsWriterServiceTests
{
    [Fact]
    public async Task ExecuteAsync_RetriesBatchAfterTransientPersistenceFailure()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB observer = new(options);
        await observer.Database.EnsureCreatedAsync();
        FailureState failureState = new();

        ServiceCollection services = new();
        services.AddScoped<DB>(_ => new FailingDb(options, failureState));
        using ServiceProvider provider = services.BuildServiceProvider();

        LogQueue queue = new(capacity: 10);
        LogsWriterService writer = new(queue, provider.GetRequiredService<IServiceScopeFactory>());
        using CancellationTokenSource cancellation = new();

        await writer.StartAsync(cancellation.Token);
        Assert.True(queue.TryEnqueue(LogsService.CreateGeneralLog("retry me", Discord.LogSeverity.Warning)));

        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline && await observer.Logs.CountAsync() == 0)
            await Task.Delay(20);

        Assert.Equal(1, await observer.Logs.CountAsync());
        Assert.Equal(
            LogsService.FormatGeneralLog("retry me", Discord.LogSeverity.Warning),
            (await observer.Logs.SingleAsync()).Message);

        cancellation.Cancel();
        await writer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesFinalBatchAfterTransientPersistenceFailure()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB observer = new(options);
        await observer.Database.EnsureCreatedAsync();
        FailureState failureState = new();

        ServiceCollection services = new();
        services.AddScoped<DB>(_ => new FailingDb(options, failureState));
        using ServiceProvider provider = services.BuildServiceProvider();

        LogQueue queue = new(capacity: 10);
        Assert.True(queue.TryEnqueue(
            LogsService.CreateGeneralLog("shutdown retry", Discord.LogSeverity.Warning)));
        LogsWriterService writer = new(queue, provider.GetRequiredService<IServiceScopeFactory>());

        await writer.StartAsync(CancellationToken.None);

        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (queue.Reader.Count > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.Equal(0, queue.Reader.Count);

        await writer.StopAsync(CancellationToken.None);

        Assert.Equal(1, await observer.Logs.CountAsync());
        Assert.Equal(
            LogsService.FormatGeneralLog("shutdown retry", Discord.LogSeverity.Warning),
            (await observer.Logs.SingleAsync()).Message);
    }

    private sealed class FailureState
    {
        public bool FailNextSave { get; set; } = true;
    }

    private sealed class FailingDb(DbContextOptions<DB> options, FailureState state) : DB(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (state.FailNextSave)
            {
                state.FailNextSave = false;
                throw new InvalidOperationException("transient persistence failure");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
