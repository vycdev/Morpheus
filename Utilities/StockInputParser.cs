using System.Globalization;

namespace Morpheus.Utilities;

internal static class StockInputParser
{
    public static bool TryParsePositiveAmount(string? input, out decimal amount)
    {
        return decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out amount) && amount > 0;
    }
}
