using System.Data;
using System.Globalization;
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

        if (dbContext.Database.CurrentTransaction != null)
        {
            await AdjustMoneySettingForUpdate(UbiPoolKey, 0m, amount);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await AdjustMoneySettingForUpdate(UbiPoolKey, 0m, amount);
        await transaction.CommitAsync();
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
        if (dbContext.Database.CurrentTransaction != null)
        {
            MoneySetting moneySetting = await GetMoneySettingForUpdate(SlotsVaultKey, SlotsSeedAmount);
            return moneySetting.Amount;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        MoneySetting setting = await GetMoneySettingForUpdate(SlotsVaultKey, SlotsSeedAmount);
        await transaction.CommitAsync();
        return setting.Amount;
    }

    /// <summary>
    /// Updates the Slots Vault by adding (positive) or removing (negative) an amount.
    /// Returns the new vault balance.
    /// </summary>
    public async Task<decimal> UpdateVault(decimal amount)
    {
        if (amount == 0) return await GetVaultAmount();

        if (dbContext.Database.CurrentTransaction != null)
        {
            return await AdjustMoneySettingForUpdate(SlotsVaultKey, SlotsSeedAmount, amount);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        decimal updated = await AdjustMoneySettingForUpdate(SlotsVaultKey, SlotsSeedAmount, amount);
        await transaction.CommitAsync();
        return updated;
    }

    /// <summary>
    /// Gets the current amount in the UBI pool.
    /// </summary>
    public async Task<decimal> GetPoolAmount()
    {
        if (dbContext.Database.CurrentTransaction != null)
        {
            MoneySetting moneySetting = await GetMoneySettingForUpdate(UbiPoolKey, 0m);
            return moneySetting.Amount;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        MoneySetting setting = await GetMoneySettingForUpdate(UbiPoolKey, 0m);
        await transaction.CommitAsync();
        return setting.Amount;
    }

    /// <summary>
    /// Distributes the UBI pool to all users.
    /// </summary>
    public async Task<string> DistributeUbi()
    {
        // Use a transaction to ensure we read and reset the pool atomically
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            MoneySetting pool = await GetMoneySettingForUpdate(UbiPoolKey, 0m);

            if (pool.Amount <= 0)
            {
                return "Pool is empty.";
            }

            int userCount = await dbContext.Users.CountAsync();
            if (userCount == 0) return "No users found.";

            decimal payoutPerUser = pool.Amount / userCount;
            
            // Round down to 2 decimal places to be safe
            payoutPerUser = Math.Floor(payoutPerUser * 100) / 100;

            if (payoutPerUser <= 0)
            {
                return $"Pool amount (${pool.Amount}) is too small to distribute among {userCount} users.";
            }

            // 1. Reset pool to remaining (dust) or 0
            // We keep the dust in the pool
            decimal distributedTotal = payoutPerUser * userCount;
            decimal remaining = pool.Amount - distributedTotal;
            
            pool.Setting.Value = FormatMoneyForStorage(remaining);
            pool.Setting.UpdateDate = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

            // 2. Bulk update users
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE \"Users\" SET \"Balance\" = \"Balance\" + {0}", 
                payoutPerUser);

            await transaction.CommitAsync();

            string msg = $"Distributed **${pool.Amount:F2}** to **{userCount}** users (**${payoutPerUser:F2}** each). Rollover: **${remaining:F2}**.";
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            MoneySetting pool = await GetMoneySettingForUpdate(UbiPoolKey, 0m);

            decimal totalTaxCollected = await dbContext.Database.SqlQueryRaw<decimal>(
                """
                WITH taxed AS (
                    UPDATE "Users"
                    SET "Balance" = "Balance" - ("Balance" * {0})
                    WHERE "Balance" > 0
                    RETURNING "Balance" * ({0} / (1 - {0})) AS "Value"
                )
                SELECT COALESCE(SUM("Value"), 0) AS "Value"
                FROM taxed
                """,
                TaxRate)
                .SingleAsync();

            if (totalTaxCollected <= 0) return;

            pool.Setting.Value = FormatMoneyForStorage(pool.Amount + totalTaxCollected);
            pool.Setting.UpdateDate = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            logsService.Log($"Wealth Tax Collected: ${totalTaxCollected:F2}", Discord.LogSeverity.Info);
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            MoneySetting pool = await GetMoneySettingForUpdate(UbiPoolKey, 0m);
            User? user = await LockUserForUpdate(userId);
            if (user == null) return (false, "User not found.");

            if (user.Balance < amount)
                return (false, $"Insufficient balance. You have **${user.Balance:F2}**.");

            // Deduct from user
            user.Balance -= amount;

            pool.Setting.Value = FormatMoneyForStorage(pool.Amount + amount);
            pool.Setting.UpdateDate = DateTime.UtcNow;

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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            Dictionary<int, User> users = await LockUsersForUpdate(robberId, victimId);
            users.TryGetValue(robberId, out User? robber);
            users.TryGetValue(victimId, out User? victim);

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

    internal async Task<User?> LockUserForUpdate(int userId)
    {
        EnsureCurrentTransaction();

        User? user = await dbContext.Users
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Users"
                WHERE "Id" = {userId}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync();

        if (user != null)
            await dbContext.Entry(user).ReloadAsync();

        return user;
    }

    internal async Task<Dictionary<int, User>> LockUsersForUpdate(params int[] userIds)
    {
        Dictionary<int, User> users = [];

        foreach (int userId in OrderUserIdsForUpdate(userIds))
        {
            User? user = await LockUserForUpdate(userId);
            if (user != null)
                users[userId] = user;
        }

        return users;
    }

    internal async Task<decimal> LockPoolForUpdate()
    {
        MoneySetting setting = await GetMoneySettingForUpdate(UbiPoolKey, 0m);
        return setting.Amount;
    }

    internal async Task<decimal> LockVaultForUpdate()
    {
        MoneySetting setting = await GetMoneySettingForUpdate(SlotsVaultKey, SlotsSeedAmount);
        return setting.Amount;
    }

    private async Task<decimal> AdjustMoneySettingForUpdate(string key, decimal defaultAmount, decimal amount)
    {
        MoneySetting setting = await GetMoneySettingForUpdate(key, defaultAmount);
        decimal updated = setting.Amount + amount;

        setting.Setting.Value = FormatMoneyForStorage(updated);
        setting.Setting.UpdateDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return updated;
    }

    private async Task<MoneySetting> GetMoneySettingForUpdate(string key, decimal defaultAmount)
    {
        EnsureCurrentTransaction();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT pg_advisory_xact_lock(hashtext({key}))""");

        List<BotSetting> settings = await dbContext.BotSettings
            .FromSqlInterpolated($"""
                SELECT *
                FROM "BotSettings"
                WHERE "Key" = {key}
                ORDER BY "Id"
                FOR UPDATE
                """)
            .ToListAsync();

        if (settings.Count == 0)
        {
            BotSetting setting = new()
            {
                Key = key,
                Value = FormatMoneyForStorage(defaultAmount),
                UpdateDate = DateTime.UtcNow
            };

            dbContext.BotSettings.Add(setting);
            await dbContext.SaveChangesAsync();

            return new MoneySetting(setting, defaultAmount);
        }

        foreach (BotSetting setting in settings)
            await dbContext.Entry(setting).ReloadAsync();

        BotSetting primary = settings[0];
        decimal amount = settings.Sum(setting => ParseMoneyFromStorage(setting.Value, defaultAmount));

        if (settings.Count > 1)
        {
            primary.Value = FormatMoneyForStorage(amount);
            primary.UpdateDate = DateTime.UtcNow;

            dbContext.BotSettings.RemoveRange(settings.Skip(1));
            await dbContext.SaveChangesAsync();
        }

        return new MoneySetting(primary, amount);
    }

    internal static IEnumerable<int> OrderUserIdsForUpdate(IEnumerable<int> userIds) =>
        userIds.Distinct().OrderBy(id => id);

    internal static decimal ParseMoneyFromStorage(string value, decimal fallback)
    {
        CultureInfo currentCulture = CultureInfo.CurrentCulture;
        string currentDecimalSeparator = currentCulture.NumberFormat.NumberDecimalSeparator;
        string invariantDecimalSeparator = CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator;

        bool valueLooksLocal =
            currentDecimalSeparator != invariantDecimalSeparator &&
            value.Contains(currentDecimalSeparator, StringComparison.Ordinal) &&
            !value.Contains(invariantDecimalSeparator, StringComparison.Ordinal);

        if (valueLooksLocal &&
            decimal.TryParse(value, NumberStyles.Number, currentCulture, out decimal localFirstAmount))
            return localFirstAmount;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal invariantAmount))
            return invariantAmount;

        if (decimal.TryParse(value, NumberStyles.Number, currentCulture, out decimal localAmount))
            return localAmount;

        return fallback;
    }

    internal static string FormatMoneyForStorage(decimal amount) =>
        amount.ToString("F2", CultureInfo.InvariantCulture);

    private void EnsureCurrentTransaction()
    {
        if (dbContext.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Economy row locks require an active database transaction.");
    }

    private sealed record MoneySetting(BotSetting Setting, decimal Amount);
}
