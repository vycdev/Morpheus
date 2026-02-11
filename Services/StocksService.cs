using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;

namespace Morpheus.Services;

public class StocksService(DB dbContext, LogsService logsService)
{
    private const decimal FeeRate = 0.05m; // 5%

    /// <summary>
    /// Gets or creates a stock for the given entity.
    /// New stocks start at $100 with a random daily update time.
    /// </summary>
    public async Task<Stock> GetOrCreateStock(StockEntityType entityType, int entityId)
    {
        Stock? stock = await dbContext.Stocks
            .FirstOrDefaultAsync(s => s.EntityType == entityType && s.EntityId == entityId);

        if (stock != null)
            return stock;

        stock = new Stock
        {
            EntityType = entityType,
            EntityId = entityId,
            Price = 100.00m,
            PreviousPrice = 100.00m,
            UpdateTimeMinutes = Random.Shared.Next(0, 1440), // random minute of day
            LastUpdatedDate = DateTime.MinValue,
            InsertDate = DateTime.UtcNow
        };

        await dbContext.Stocks.AddAsync(stock);
        await dbContext.SaveChangesAsync();

        logsService.Log($"New stock created: {entityType} #{entityId}", Discord.LogSeverity.Verbose);

        return stock;
    }

    /// <summary>
    /// Buy shares of a stock. Deducts amount from user balance, applies 5% fee,
    /// and purchases shares with the remaining 95%.
    /// </summary>
    public async Task<(bool success, string message, decimal sharesBought)> BuyStock(int userId, int stockId, decimal amount)
    {
        User? user = await dbContext.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.", 0);

        Stock? stock = await dbContext.Stocks.FindAsync(stockId);
        if (stock == null) return (false, "Stock not found.", 0);

        if (amount <= 0) return (false, "Amount must be positive.", 0);
        if (user.Balance < amount) return (false, $"Insufficient balance. You have **${user.Balance:F2}**.", 0);
        if (stock.Price <= 0) return (false, "Stock price is invalid.", 0);

        decimal fee = amount * FeeRate;
        decimal investedAmount = amount - fee;
        decimal sharesBought = investedAmount / stock.Price;

        // Deduct from balance
        user.Balance -= amount;

        // Get or create holding
        StockHolding? holding = await dbContext.StockHoldings
            .FirstOrDefaultAsync(sh => sh.UserId == userId && sh.StockId == stockId);

        if (holding == null)
        {
            holding = new StockHolding
            {
                UserId = userId,
                StockId = stockId,
                Shares = sharesBought,
                TotalInvested = investedAmount,
                InsertDate = DateTime.UtcNow
            };
            await dbContext.StockHoldings.AddAsync(holding);
        }
        else
        {
            holding.Shares += sharesBought;
            holding.TotalInvested += investedAmount;
        }

        // Record transaction
        StockTransaction transaction = new()
        {
            UserId = userId,
            StockId = stockId,
            Type = TransactionType.StockBuy,
            Amount = amount,
            Fee = fee,
            Shares = sharesBought,
            PriceAtTransaction = stock.Price,
            InsertDate = DateTime.UtcNow
        };
        await dbContext.StockTransactions.AddAsync(transaction);

        await dbContext.SaveChangesAsync();

        return (true, $"Bought **{sharesBought:F4}** shares at **${stock.Price:F2}**/share.\nFee: **${fee:F2}** | Invested: **${investedAmount:F2}**", sharesBought);
    }

    /// <summary>
    /// Sell shares of a stock. Applies 5% fee on proceeds and credits the net to balance.
    /// Pass null for sharesToSell to sell all shares.
    /// </summary>
    public async Task<(bool success, string message, decimal proceeds)> SellStock(int userId, int stockId, decimal? sharesToSell)
    {
        User? user = await dbContext.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.", 0);

        Stock? stock = await dbContext.Stocks.FindAsync(stockId);
        if (stock == null) return (false, "Stock not found.", 0);

        StockHolding? holding = await dbContext.StockHoldings
            .FirstOrDefaultAsync(sh => sh.UserId == userId && sh.StockId == stockId);

        if (holding == null || holding.Shares <= 0)
            return (false, "You don't own any shares of this stock.", 0);

        decimal actualShares = sharesToSell ?? holding.Shares;
        if (actualShares <= 0) return (false, "Amount must be positive.", 0);
        if (actualShares > holding.Shares)
            return (false, $"You only own **{holding.Shares:F4}** shares.", 0);

        decimal grossProceeds = actualShares * stock.Price;
        decimal fee = grossProceeds * FeeRate;
        decimal netProceeds = grossProceeds - fee;

        // Credit balance
        user.Balance += netProceeds;

        // Update or remove holding
        decimal investedRatio = actualShares / holding.Shares;
        decimal investedReturned = holding.TotalInvested * investedRatio;

        holding.Shares -= actualShares;
        holding.TotalInvested -= investedReturned;

        if (holding.Shares <= 0.000001m) // float cleanup
        {
            holding.Shares = 0;
            holding.TotalInvested = 0;
        }

        // Record transaction
        StockTransaction transaction = new()
        {
            UserId = userId,
            StockId = stockId,
            Type = TransactionType.StockSell,
            Amount = netProceeds,
            Fee = fee,
            Shares = actualShares,
            PriceAtTransaction = stock.Price,
            InsertDate = DateTime.UtcNow
        };
        await dbContext.StockTransactions.AddAsync(transaction);

        await dbContext.SaveChangesAsync();

        return (true, $"Sold **{actualShares:F4}** shares at **${stock.Price:F2}**/share.\nGross: **${grossProceeds:F2}** | Fee: **${fee:F2}** | Net: **${netProceeds:F2}**", netProceeds);
    }

    /// <summary>
    /// Transfer money between users. Sender pays amount + 5% fee, receiver gets the amount.
    /// </summary>
    public async Task<(bool success, string message)> TransferMoney(int fromUserId, int toUserId, decimal amount)
    {
        if (fromUserId == toUserId) return (false, "You can't transfer money to yourself.");
        if (amount <= 0) return (false, "Amount must be positive.");

        User? sender = await dbContext.Users.FindAsync(fromUserId);
        if (sender == null) return (false, "Sender not found.");

        User? receiver = await dbContext.Users.FindAsync(toUserId);
        if (receiver == null) return (false, "Receiver not found.");

        decimal fee = amount * FeeRate;
        decimal totalCost = amount + fee;

        if (sender.Balance < totalCost)
            return (false, $"Insufficient balance. You need **${totalCost:F2}** (${amount:F2} + ${fee:F2} fee) but have **${sender.Balance:F2}**.");

        sender.Balance -= totalCost;
        receiver.Balance += amount;

        // Record transaction
        StockTransaction transaction = new()
        {
            UserId = fromUserId,
            TargetUserId = toUserId,
            Type = TransactionType.Transfer,
            Amount = amount,
            Fee = fee,
            InsertDate = DateTime.UtcNow
        };
        await dbContext.StockTransactions.AddAsync(transaction);

        await dbContext.SaveChangesAsync();

        return (true, $"Transferred **${amount:F2}** to the recipient.\nFee: **${fee:F2}** | Total cost: **${totalCost:F2}**");
    }

    /// <summary>
    /// Calculate and update the stock price based on entity XP activity.
    /// Compares yesterday's total XP to the day before yesterday.
    /// Change is clamped to ±10%.
    /// </summary>
    public async Task UpdateStockPrice(Stock stock, DateTime utcNow)
    {
        DateOnly today = DateOnly.FromDateTime(utcNow);
        DateOnly yesterday = today.AddDays(-1);
        DateOnly dayBefore = today.AddDays(-2);

        long xpYesterday = await GetEntityXpForDate(stock.EntityType, stock.EntityId, yesterday);
        long xpDayBefore = await GetEntityXpForDate(stock.EntityType, stock.EntityId, dayBefore);

        decimal changePercent;

        if (xpDayBefore == 0 && xpYesterday == 0)
        {
            changePercent = 0m;
        }
        else if (xpDayBefore == 0)
        {
            changePercent = 10m; // cap at +10%
        }
        else if (xpYesterday == 0)
        {
            changePercent = -10m; // cap at -10%
        }
        else
        {
            changePercent = ((decimal)(xpYesterday - xpDayBefore) / xpDayBefore) * 100m;
            changePercent = Math.Clamp(changePercent, -10m, 10m);
        }

        stock.PreviousPrice = stock.Price;
        stock.Price *= (1m + changePercent / 100m);

        // Ensure price doesn't go below 0.01
        if (stock.Price < 0.01m)
            stock.Price = 0.01m;

        stock.DailyChangePercent = changePercent;
        stock.LastUpdatedDate = utcNow;
    }

    /// <summary>
    /// Get total XP for a given entity on a specific date (UTC day boundaries).
    /// </summary>
    private async Task<long> GetEntityXpForDate(StockEntityType entityType, int entityId, DateOnly date)
    {
        DateTime dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime dayEnd = dayStart.AddDays(1);

        return entityType switch
        {
            StockEntityType.User => await dbContext.UserActivity
                .Where(ua => ua.UserId == entityId && ua.InsertDate >= dayStart && ua.InsertDate < dayEnd)
                .SumAsync(ua => (long)ua.XpGained),

            StockEntityType.Guild => await dbContext.UserActivity
                .Where(ua => ua.GuildId == entityId && ua.InsertDate >= dayStart && ua.InsertDate < dayEnd)
                .SumAsync(ua => (long)ua.XpGained),

            StockEntityType.Channel => await GetChannelXpForDate(entityId, dayStart, dayEnd),

            _ => 0
        };
    }

    private async Task<long> GetChannelXpForDate(int channelEntityId, DateTime dayStart, DateTime dayEnd)
    {
        Channel? channel = await dbContext.Channels.FindAsync(channelEntityId);
        if (channel == null) return 0;

        return await dbContext.UserActivity
            .Where(ua => ua.DiscordChannelId == channel.DiscordId && ua.InsertDate >= dayStart && ua.InsertDate < dayEnd)
            .SumAsync(ua => (long)ua.XpGained);
    }

    /// <summary>
    /// Get the top movers (biggest daily % changes) across all stocks.
    /// Returns (gainers, losers) each sorted by magnitude.
    /// </summary>
    public async Task<(List<Stock> gainers, List<Stock> losers)> GetTopMovers(int count = 5)
    {
        // Only stocks that have been updated today
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateTime todayStart = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var gainers = await dbContext.Stocks
            .Where(s => s.LastUpdatedDate >= todayStart && s.DailyChangePercent > 0)
            .OrderByDescending(s => s.DailyChangePercent)
            .Take(count)
            .ToListAsync();

        var losers = await dbContext.Stocks
            .Where(s => s.LastUpdatedDate >= todayStart && s.DailyChangePercent < 0)
            .OrderBy(s => s.DailyChangePercent)
            .Take(count)
            .ToListAsync();

        return (gainers, losers);
    }

    /// <summary>
    /// Resolves the display name for a stock entity.
    /// </summary>
    public async Task<string> GetStockDisplayName(Stock stock)
    {
        return stock.EntityType switch
        {
            StockEntityType.User => (await dbContext.Users.FindAsync(stock.EntityId))?.Username ?? $"User #{stock.EntityId}",
            StockEntityType.Guild => (await dbContext.Guilds.FindAsync(stock.EntityId))?.Name ?? $"Guild #{stock.EntityId}",
            StockEntityType.Channel => (await dbContext.Channels.FindAsync(stock.EntityId))?.Name ?? $"Channel #{stock.EntityId}",
            _ => $"Unknown #{stock.EntityId}"
        };
    }

    /// <summary>
    /// Gets all stock holdings for a user with their stock data included.
    /// </summary>
    public async Task<List<StockHolding>> GetUserHoldings(int userId)
    {
        return await dbContext.StockHoldings
            .Include(sh => sh.Stock)
            .Where(sh => sh.UserId == userId && sh.Shares > 0)
            .ToListAsync();
    }
}
