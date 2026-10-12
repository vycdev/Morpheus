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

    [Fact]
    public async Task ExecuteAsync_FlushesMoreThanOneBatchOnShutdown()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB observer = new(options);
        await observer.Database.EnsureCreatedAsync();

        ShutdownState shutdown = new();
        using ServiceProvider provider = new ServiceCollection()
            .AddScoped<DB>(_ => new ShutdownDb(options, shutdown))
            .BuildServiceProvider();
        LogQueue queue = new(capacity: 125);
        for (int i = 0; i < 125; i++)
            Assert.True(queue.TryEnqueue(LogsService.CreateGeneralLog($"shutdown {i}", Discord.LogSeverity.Info)));

        using LogsWriterService writer = new(queue, provider.GetRequiredService<IServiceScopeFactory>());
        await writer.StartAsync(CancellationToken.None);
        try
        {
            // Hold the first save until StopAsync cancels the worker. This leaves
            // all 125 entries for the final flush, regardless of scheduler speed.
            await shutdown.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(75, queue.Reader.Count);
            Assert.Equal(0, await observer.Logs.CountAsync());
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        }

        string[] messages = await observer.Logs.OrderBy(log => log.Id)
            .Select(log => log.Message).ToArrayAsync();
        Assert.Equal(125, messages.Length);
        Assert.Equal(0, queue.Reader.Count);
        Assert.Equal(new[] { 50, 50, 25 }, shutdown.FinalBatchSizes);
        Assert.Equal(
            Enumerable.Range(0, 125)
                .Select(i => LogsService.FormatGeneralLog($"shutdown {i}", Discord.LogSeverity.Info)),
            messages);
    }

    private sealed class ShutdownState
    {
        public TaskCompletionSource FirstSaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<int> FinalBatchSizes { get; } = [];
    }

    private sealed class ShutdownDb(DbContextOptions<DB> options, ShutdownState state) : DB(options)
    {
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (state.FirstSaveStarted.TrySetResult())
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            Assert.False(cancellationToken.CanBeCanceled);
            int count = await base.SaveChangesAsync(cancellationToken);
            state.FinalBatchSizes.Add(count);
            return count;
        }
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
