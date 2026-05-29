using Morpheus.Database.Enums;
using Morpheus.Services;

namespace Morpheus.Tests;

public class StocksServiceTests
{
    [Fact]
    public void OrderStockHoldingKeysForUpdate_DeduplicatesAndSortsByStockThenUser()
    {
        (int UserId, int StockId)[] orderedKeys =
        [
            .. StocksService.OrderStockHoldingKeysForUpdate(
            [
                (UserId: 10, StockId: 3),
                (UserId: 4, StockId: 2),
                (UserId: 4, StockId: 2),
                (UserId: 1, StockId: 3),
                (UserId: 9, StockId: 1)
            ])
        ];

        Assert.Equal(
        [
            (UserId: 9, StockId: 1),
            (UserId: 4, StockId: 2),
            (UserId: 1, StockId: 3),
            (UserId: 10, StockId: 3)
        ], orderedKeys);
    }

    [Fact]
    public void BuildStockEntityLockKey_IncludesEntityTypeAndId()
    {
        string key = StocksService.BuildStockEntityLockKey(StockEntityType.Channel, 123);

        Assert.Equal($"stock:{(int)StockEntityType.Channel}:123", key);
    }

    [Fact]
    public void BuildStockHoldingLockKey_OrdersStockBeforeUser()
    {
        string key = StocksService.BuildStockHoldingLockKey(userId: 7, stockId: 99);

        Assert.Equal("stockholding:99:7", key);
    }
}
