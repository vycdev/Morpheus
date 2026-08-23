using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Morpheus.Database;
using Morpheus.Database.Models;

namespace Morpheus.Services;

public sealed class LogsWriterService(LogQueue logQueue, IServiceScopeFactory scopeFactory) : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxFinalFlushAttempts = 3;
    private static readonly TimeSpan BatchDelay = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<QueuedLog> batch = new(BatchSize);

        try
        {
            while (await WaitForBatchAsync(batch, stoppingToken))
            {
                if (batch.Count < BatchSize)
                {
                    try
                    {
                        await Task.Delay(BatchDelay, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                    }

                    DrainAvailable(batch);
                }

                while (batch.Count > 0)
                {
                    if (await FlushAsync(batch, stoppingToken))
                        break;

                    try
                    {
                        await Task.Delay(BatchDelay, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            int failedFlushAttempts = 0;
            while (true)
            {
                DrainAvailable(batch);
                if (batch.Count == 0)
                    break;

                if (await FlushAsync(batch, CancellationToken.None))
                {
                    failedFlushAttempts = 0;
                    continue;
                }

                failedFlushAttempts++;
                if (failedFlushAttempts >= MaxFinalFlushAttempts)
                    break;

                await Task.Delay(BatchDelay);
            }
        }
    }

    private async Task<bool> WaitForBatchAsync(List<QueuedLog> batch, CancellationToken cancellationToken)
    {
        if (batch.Count > 0)
            return true;

        if (!await logQueue.Reader.WaitToReadAsync(cancellationToken))
            return false;

        DrainAvailable(batch);
        return batch.Count > 0;
    }

    private void DrainAvailable(List<QueuedLog> batch)
    {
        while (batch.Count < BatchSize && logQueue.Reader.TryRead(out QueuedLog? log))
            batch.Add(log);
    }

    private async Task<bool> FlushAsync(List<QueuedLog> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return true;

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            DB dbContext = scope.ServiceProvider.GetRequiredService<DB>();

            dbContext.Logs.AddRange(batch.Select(CreateLogEntity));

            await dbContext.SaveChangesAsync(cancellationToken);
            batch.Clear();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(LogsService.FormatGeneralLog(
                $"Failed to persist {batch.Count} queued log entries: {ex.Message}",
                Discord.LogSeverity.Error));

            return false;
        }
    }

    internal static Log CreateLogEntity(QueuedLog log) =>
        new()
        {
            Message = log.Message,
            Severity = log.Severity,
            Version = log.Version,
            InsertDate = log.InsertDate
        };
}
