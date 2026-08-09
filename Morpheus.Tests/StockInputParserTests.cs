using System.Globalization;
using Morpheus.Utilities;

namespace Morpheus.Tests;

public class StockInputParserTests
{
    [Fact]
    public void TryParsePositiveAmount_UsesInvariantDecimalSeparator()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ",";
            culture.NumberFormat.NumberGroupSeparator = ".";
            CultureInfo.CurrentCulture = culture;

            bool parsed = StockInputParser.TryParsePositiveAmount("12.50", out decimal amount);

            Assert.True(parsed);
            Assert.Equal(12.50m, amount);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    public void TryParsePositiveAmount_RejectsMissingAndNonPositiveValues(string? input)
    {
        Assert.False(StockInputParser.TryParsePositiveAmount(input, out _));
    }
}
