using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Enums;
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
        int affected = await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE \"BotSettings\" SET \"Value\" = CAST((CAST(\"Value\" AS DECIMAL) + {0}) AS TEXT) WHERE \"Key\" = {1}",
            amount, UbiPoolKey);

        // If no row was updated, it means the key doesn't exist yet. Initialize it.
        if (affected == 0)
        {
            var existing = await dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == UbiPoolKey);
            if (existing == null)
            {
                dbContext.BotSettings.Add(new BotSetting
                {
                    Key = UbiPoolKey,
                    Value = amount.ToString("F2"),
                    UpdateDate = DateTime.UtcNow
                });
                await dbContext.SaveChangesAsync();
            }
            else
            {
                // It was created concurrently, retry the update
                await dbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE \"BotSettings\" SET \"Value\" = CAST((CAST(\"Value\" AS DECIMAL) + {0}) AS TEXT) WHERE \"Key\" = {1}",
                    amount, UbiPoolKey);
            }
        }
    }

    // ── SLOTS VAULT ──

    private const string SlotsVaultKey = "slots_vault";
    private const decimal SlotsSeedAmount = 10000.00m;

    /// <summary>
    /// Gets the current amount in the Slots Vault.
    /// If it doesn't exist, it seeds it with $10,000.
    /// </summary>
    public async Task<decimal> GetVaultAmount()
    {
        BotSetting? setting = await dbContext.BotSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == SlotsVaultKey);

        if (setting == null)
        {
            // Seed the vault
            setting = new BotSetting { Key = SlotsVaultKey, Value = SlotsSeedAmount.ToString("F2") };
            dbContext.BotSettings.Add(setting);
            await dbContext.SaveChangesAsync();
            return SlotsSeedAmount;
        }

        if (decimal.TryParse(setting.Value, out decimal vault))
        {
            return vault;
        }

        return SlotsSeedAmount; // Fallback
    }

    /// <summary>
    /// Updates the Slots Vault by adding (positive) or removing (negative) an amount.
    /// Returns the new vault balance.
    /// </summary>
    public async Task<decimal> UpdateVault(decimal amount)
    {
        if (amount == 0) return await GetVaultAmount();

        // Atomic update
        int affected = await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE \"BotSettings\" SET \"Value\" = CAST((CAST(\"Value\" AS DECIMAL) + {0}) AS TEXT) WHERE \"Key\" = {1}",
            amount, SlotsVaultKey);

        if (affected == 0)
        {
            // Initialize if missing
            await GetVaultAmount();
            // Retry update
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE \"BotSettings\" SET \"Value\" = CAST((CAST(\"Value\" AS DECIMAL) + {0}) AS TEXT) WHERE \"Key\" = {1}",
                amount, SlotsVaultKey);
        }

        return await GetVaultAmount();
    }

    /// <summary>
    /// Gets the current amount in the UBI pool.
    /// </summary>
    public async Task<decimal> GetPoolAmount()
    {
        BotSetting? setting = await dbContext.BotSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == UbiPoolKey);
        
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

    /// <summary>
    /// Collects a 0.01% wealth tax from all users' liquid balances and adds it to the UBI pool.
    /// </summary>
    public async Task CollectWealthTax()
    {
        const decimal TaxRate = 0.0001m; // 0.01%

        // 1. Calculate total tax to collect using SQL for performance
        // We sum up (Balance * 0.0001) for all users where Balance > 0
        // We can do this in one go: Update balance and return the deducted amount?
        // Postgres supports UPDATE ... RETURNING, but EF Core ExecuteSqlRaw doesn't return the result set easily.
        
        // Strategy:
        // A. Calculate Total Taxable Liquidity
        // B. Update Users (Balance = Balance * 0.9999)
        // C. Add difference to Pool

        using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            // Get total liquid money in the system (only positive balances)
            decimal totalLiquidity = await dbContext.Users
                .Where(u => u.Balance > 0)
                .SumAsync(u => u.Balance);

            if (totalLiquidity <= 0) return;

            decimal totalTaxCollected = totalLiquidity * TaxRate;

            // Apply tax: specific update for all users with money
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE \"Users\" SET \"Balance\" = \"Balance\" - (\"Balance\" * {0}) WHERE \"Balance\" > 0",
                TaxRate);

            // Add to pool
            // We use the helper method but since we are in a transaction we need to be careful not to deadlock
            // But AddToPool uses a separate atomic query. 
            // Better to do it manually inside this transaction for consistency.

            BotSetting? setting = await dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == UbiPoolKey);
            if (setting == null)
            {
                setting = new BotSetting { Key = UbiPoolKey, Value = "0.00" };
                dbContext.BotSettings.Add(setting);
            }

            if (decimal.TryParse(setting.Value, out decimal currentPool))
            {
                setting.Value = (currentPool + totalTaxCollected).ToString("F2");
                setting.UpdateDate = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            logsService.Log($"Wealth Tax Collected: ${totalTaxCollected:F2} from total liquidity ${totalLiquidity:F2}", Discord.LogSeverity.Info);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logsService.Log($"Error collecting Wealth Tax: {ex.Message}", Discord.LogSeverity.Error);
        }
    }

    /// <summary>
    /// Donate money to the UBI pool.
    /// </summary>
    public async Task<(bool success, string message)> DonateToUbi(int userId, decimal amount)
    {
        if (amount <= 0) return (false, "Amount must be positive.");

        using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            User? user = await dbContext.Users.FindAsync(userId);
            if (user == null) return (false, "User not found.");

            if (user.Balance < amount)
                return (false, $"Insufficient balance. You have **${user.Balance:F2}**.");

            // Deduct from user
            user.Balance -= amount;

            // Add to pool
            // We reuse the logic but implement it inline for the transaction safety
            BotSetting? setting = await dbContext.BotSettings.FirstOrDefaultAsync(s => s.Key == UbiPoolKey);
            if (setting == null)
            {
                setting = new BotSetting { Key = UbiPoolKey, Value = "0.00" };
                dbContext.BotSettings.Add(setting);
            }

            if (decimal.TryParse(setting.Value, out decimal currentPool))
            {
                setting.Value = (currentPool + amount).ToString("F2");
                setting.UpdateDate = DateTime.UtcNow;
            }

            // Record transaction
            StockTransaction stockTransaction = new()
            {
                UserId = userId,
                Type = TransactionType.Donation,
                Amount = amount,
                Fee = 0,
                InsertDate = DateTime.UtcNow
            };
            await dbContext.StockTransactions.AddAsync(stockTransaction);

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            logsService.Log($"User #{userId} donated ${amount:F2} to UBI pool.", Discord.LogSeverity.Info);
            return (true, $"You donated **${amount:F2}** to the UBI pool! Thank you for your generosity.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logsService.Log($"Error processing donation: {ex.Message}", Discord.LogSeverity.Error);
            return (false, "An error occurred while processing your donation.");
        }
    }

    /// <summary>
    /// Attempts to rob a target user.
    /// Rules:
    /// 1. 1h Cooldown per robber.
    /// 2. 40% Success Chance (20% if victim was robbed in last 24h).
    /// 3. Success: Steal 1% (5% Crit).
    /// 4. Failure: Pay victim 1%.
    /// </summary>
    public async Task<(bool success, string message)> RobUser(int robberId, int victimId)
    {
        if (robberId == victimId) return (false, "You cannot rob yourself.");

        using var transaction = await dbContext.Database.BeginTransactionAsync();
        try
        {
            User? robber = await dbContext.Users.FindAsync(robberId);
            User? victim = await dbContext.Users.FindAsync(victimId);

            if (robber == null || victim == null) return (false, "User not found.");

            // 1. Check Cooldown
            if (DateTime.UtcNow < robber.LastRobberyAttempt.AddHours(1))
            {
                TimeSpan remaining = robber.LastRobberyAttempt.AddHours(1) - DateTime.UtcNow;
                string timeStr = remaining.Hours > 0 ? $"{remaining.Hours}h {remaining.Minutes}m" : $"{remaining.Minutes}m {remaining.Seconds}s";
                return (false, $"You are laying low. You can rob again in **{timeStr}**.");
            }

            // 2. Determine Success Chance
            bool isParanoid = DateTime.UtcNow < victim.LastSuccessfullyRobbed.AddHours(24);
            int successChance = isParanoid ? 20 : 40;

            // 3. Roll
            int roll = Random.Shared.Next(0, 100);
            bool isSuccess = roll < successChance;

            // Update timestamp immediately
            robber.LastRobberyAttempt = DateTime.UtcNow;

            if (isSuccess)
            {
                // CRIT CHECK (10% chance relative to success, or absolute? "low 10% chance on top of the 40%")
                // Interpreting "low 10% on top" as: IF successful, check for crit.
                // Let's say 10% chance of Crit.
                bool isCrit = Random.Shared.Next(0, 100) < 10;
                decimal stealPercent = isCrit ? 0.05m : 0.01m;

                decimal stolenAmount = Math.Floor(victim.Balance * stealPercent * 100) / 100; // Round down to cents
                if (stolenAmount < 0.01m) stolenAmount = 0;

                if (stolenAmount > 0)
                {
                    victim.Balance -= stolenAmount;
                    robber.Balance += stolenAmount;
                    victim.LastSuccessfullyRobbed = DateTime.UtcNow;

                    // Log Transactions
                    await dbContext.StockTransactions.AddAsync(new StockTransaction
                    {
                        UserId = robberId,
                        TargetUserId = victimId,
                        Type = TransactionType.RobberyWin,
                        Amount = stolenAmount,
                        Fee = 0,
                        InsertDate = DateTime.UtcNow
                    });
                }
                
                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                string verb = isCrit ? "**HEIST!** You pulled off a master plan" : "You successfully pickpocketed";
                return (true, $"🥷 {verb} and stole **${stolenAmount:F2}** from **{victim.Username}**!");
            }
            else
            {
                // FAILURE: Pay 1% of ROBBER's balance to VICTIM
                decimal penaltyAmount = Math.Floor(robber.Balance * 0.01m * 100) / 100;
                if (penaltyAmount < 0.01m) penaltyAmount = 0;

                if (penaltyAmount > 0)
                {
                    robber.Balance -= penaltyAmount;
                    victim.Balance += penaltyAmount;

                    // Log Transactions
                    await dbContext.StockTransactions.AddAsync(new StockTransaction
                    {
                        UserId = robberId, // The person who lost money
                        TargetUserId = victimId,
                        Type = TransactionType.RobberyLoss,
                        Amount = penaltyAmount, // Negative? No, amount is usually absolute, Type defines direction.
                                                // Wait, usually Amount is money moving.
                                                // For a Loss, Robber loses Amount.
                        Fee = 0,
                        InsertDate = DateTime.UtcNow
                    });
                }

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, $"👮 **BUSTED!** You were caught trying to rob **{victim.Username}** and had to pay them **${penaltyAmount:F2}** as a settlement.");
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logsService.Log($"Error processing robbery: {ex.Message}", Discord.LogSeverity.Error);
            return (false, "An error occurred while attempting the robbery.");
        }
    }

    /// <summary>
    /// Gets the top donors to the UBI pool.
    /// </summary>
    public async Task<List<(string Username, decimal TotalDonated)>> GetTopDonors(int count = 10)
    {
        var topDonors = await dbContext.StockTransactions
            .AsNoTracking()
            .Where(t => t.Type == TransactionType.Donation)
            .GroupBy(t => t.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalDonated = g.Sum(t => t.Amount)
            })
            .OrderByDescending(x => x.TotalDonated)
            .Take(count)
            .ToListAsync();

        var userIds = topDonors.Select(x => x.UserId).ToList();
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username);

        return topDonors
            .Select(x => (users.GetValueOrDefault(x.UserId, "Unknown"), x.TotalDonated))
            .ToList();
    }
}