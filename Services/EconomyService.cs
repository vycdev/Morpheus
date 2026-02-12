using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;

namespace Morpheus.Services;

public class EconomyService(DB dbContext, LogsService logsService)
{
    private const string UbiPoolKey = "ubi_pool";

    /// <summary>
    /// atomically adds an amount to the UBI pool.
    /// </summary>
    public async Task AddToPool(decimal amount)
    {
        if (amount <= 0) return;

        // Atomic update using raw SQL to prevent race conditions
        // We cast to text because the Value column is a string
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE \"BotSettings\" SET \"Value\" = CAST((CAST(\"Value\" AS DECIMAL) + {0}) AS TEXT) WHERE \"Key\" = {1}",
            amount, UbiPoolKey);
    }

    /// <summary>
    /// Gets the current amount in the UBI pool.
    /// </summary>
    public async Task<decimal> GetPoolAmount()
    {
        BotSetting? setting = await dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == UbiPoolKey);
        
        if (setting == null)
        {
            // Initialize if not exists
            setting = new BotSetting { Key = UbiPoolKey, Value = "0.00" };
            dbContext.BotSettings.Add(setting);
            await dbContext.SaveChangesAsync();
            return 0m;
        }

        if (decimal.TryParse(setting.Value, out decimal pool))
        {
            return pool;
        }

        return 0m;
    }

    /// <summary>
    /// Distributes the UBI pool to all users.
    /// </summary>
    public async Task<string> DistributeUbi()
    {
        // Use a transaction to ensure we read and reset the pool atomically
        using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            // Lock the row and get current value
            // We use a raw query to ensure we get the latest value and lock it if possible, 
            // but EF Core's transaction is usually enough for this level of concurrency.
            // For extra safety, we can re-fetch.
            
            BotSetting? setting = await dbContext.BotSettings
                .FirstOrDefaultAsync(s => s.Key == UbiPoolKey);

            if (setting == null || !decimal.TryParse(setting.Value, out decimal poolAmount) || poolAmount <= 0)
            {
                return "Pool is empty.";
            }

            int userCount = await dbContext.Users.CountAsync();
            if (userCount == 0) return "No users found.";

            decimal payoutPerUser = poolAmount / userCount;
            
            // Round down to 2 decimal places to be safe
            payoutPerUser = Math.Floor(payoutPerUser * 100) / 100;

            if (payoutPerUser <= 0)
            {
                return $"Pool amount (${poolAmount}) is too small to distribute among {userCount} users.";
            }

            // 1. Reset pool to remaining (dust) or 0
            // We keep the dust in the pool
            decimal distributedTotal = payoutPerUser * userCount;
            decimal remaining = poolAmount - distributedTotal;
            
            setting.Value = remaining.ToString("F2");
            setting.UpdateDate = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            // 2. Bulk update users
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE \"Users\" SET \"Balance\" = \"Balance\" + {0}", 
                payoutPerUser);

            await transaction.CommitAsync();

            string msg = $"Distributed **${poolAmount:F2}** to **{userCount}** users (**${payoutPerUser:F2}** each). Rollover: **${remaining:F2}**.";
            logsService.Log(msg, Discord.LogSeverity.Info);
            return msg;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logsService.Log($"Error distributing UBI: {ex.Message}", Discord.LogSeverity.Error);
            return $"Error: {ex.Message}";
        }
    }
}
