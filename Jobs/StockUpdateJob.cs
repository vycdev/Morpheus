using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

[DisallowConcurrentExecution]
public class StockUpdateJob(LogsService logsService, DB db, StocksService stocksService) : IJob
{
    private void Log(string message) =>
        logsService.Log($"Quartz Job - {message}");

    public async Task Execute(IJobExecutionContext context)
    {
        DateTime utcNow = DateTime.UtcNow;
        DateOnly today = DateOnly.FromDateTime(utcNow);
        DateTime todayStart = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        int currentMinutes = utcNow.Hour * 60 + utcNow.Minute;

        // Find stocks that are due for update:
        // - Haven't been updated today (LastUpdatedDate < start of today)
        // - Their scheduled time has passed (UpdateTimeMinutes <= current minute of day)
        List<Stock> stocksToUpdate = await db.Stocks
            .Where(s => s.LastUpdatedDate < todayStart && s.UpdateTimeMinutes <= currentMinutes)
            .ToListAsync();

        if (stocksToUpdate.Count == 0)
            return;

        Log($"Updating {stocksToUpdate.Count} stock prices...");

        int updated = 0;
        const int batchSize = 50;

        for (int i = 0; i < stocksToUpdate.Count; i += batchSize)
        {
            var batch = stocksToUpdate.Skip(i).Take(batchSize);

            foreach (Stock stock in batch)
            {
                await stocksService.UpdateStockPrice(stock, utcNow);
                updated++;
            }

            await db.SaveChangesAsync();

            // Small delay between batches to avoid blocking
            if (i + batchSize < stocksToUpdate.Count)
                await Task.Delay(50);
        }

        Log($"Updated {updated} stock prices.");
    }
}
