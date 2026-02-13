using System.ComponentModel.DataAnnotations;

namespace Morpheus.Database.Enums;

public enum TransactionType
{
    [Display(Name = "Stock Buy")]
    StockBuy = 1,

    [Display(Name = "Stock Sell")]
    StockSell = 2,

    [Display(Name = "Transfer")]
    Transfer = 3,

    [Display(Name = "Slots Win")]
    SlotsWin = 4,

    [Display(Name = "Slots Loss")]
    SlotsLoss = 5,

    [Display(Name = "Donation")]
    Donation = 6,

    [Display(Name = "Robbery Win")]
    RobberyWin = 7,

    [Display(Name = "Robbery Loss")]
    RobberyLoss = 8,

    [Display(Name = "Stock Transfer")]
    StockTransfer = 9
}
