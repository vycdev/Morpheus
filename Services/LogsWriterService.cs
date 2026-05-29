using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Morpheus.Database;
using Morpheus.Database.Models;

namespace Morpheus.Services;

public sealed class LogsWriterService(LogQueue logQueue, IServiceScopeFactory scopeFactory) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan BatchDelay = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<QueuedLog> batch = new(BatchSize);

        try
        {
            while (await logQueue.Reader.WaitToReadAsync(stoppingToken))
            {
                DrainAvailable(batch);

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

                await FlushAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            while (true)
            {
                DrainAvailable(batch);
                if (batch.Count == 0)
                    break;

                await FlushAsync(batch, CancellationToken.None);
            }
        }
    }

    private void DrainAvailable(List<QueuedLog> batch)
    {
        while (batch.Count < BatchSize && logQueue.Reader.TryRead(out QueuedLog? log))
            batch.Add(log);
    }

    private async Task FlushAsync(List<QueuedLog> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return;

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            DB dbContext = scope.ServiceProvider.GetRequiredService<DB>();

            dbContext.Logs.AddRange(batch.Select(CreateLogEntity));

            await dbContext.SaveChangesAsync(cancellationToken);
            batch.Clear();
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

            batch.Clear();
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
