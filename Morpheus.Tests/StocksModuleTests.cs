using System.Globalization;
using Morpheus.Modules;

namespace Morpheus.Tests;

public class StocksModuleTests
{
    [Fact]
    public void TryParseShareAmount_UsesInvariantDecimalSeparator()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo commaDecimalCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        commaDecimalCulture.NumberFormat.NumberDecimalSeparator = ",";
        commaDecimalCulture.NumberFormat.NumberGroupSeparator = ".";

        try
        {
            CultureInfo.CurrentCulture = commaDecimalCulture;

            bool parsed = StocksModule.TryParseShareAmount("12.50", out decimal amount);

            Assert.True(parsed);
            Assert.Equal(12.50m, amount);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("12,50")]
    [InlineData("1,000")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void TryParseShareAmount_RejectsAmbiguousOrNonPositiveValues(string value)
    {
        Assert.False(StocksModule.TryParseShareAmount(value, out _));
    }
}
