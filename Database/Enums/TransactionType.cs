using System.ComponentModel.DataAnnotations;

namespace Morpheus.Database.Enums;

public enum TransactionType
{
    [Display(Name = "Stock Buy")]
    StockBuy = 1,

    [Display(Name = "Stock Sell")]
    StockSell = 2,

    [Display(Name = "Transfer")]
    Transfer = 3
}
