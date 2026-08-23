using System.Globalization;
using Morpheus.Modules;

namespace Morpheus.Tests;

public class SlotsModuleTests
{
    [Fact]
    public void FormatButtonBet_UsesInvariantDecimalSeparator()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo commaDecimalCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        commaDecimalCulture.NumberFormat.NumberDecimalSeparator = ",";
        commaDecimalCulture.NumberFormat.NumberGroupSeparator = ".";

        try
        {
            CultureInfo.CurrentCulture = commaDecimalCulture;

            Assert.Equal("12.50", SlotsModule.FormatButtonBet(12.50m));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void TryParseButtonBet_UsesInvariantDecimalSeparator()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo commaDecimalCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        commaDecimalCulture.NumberFormat.NumberDecimalSeparator = ",";
        commaDecimalCulture.NumberFormat.NumberGroupSeparator = ".";

        try
        {
            CultureInfo.CurrentCulture = commaDecimalCulture;

            bool parsed = SlotsModule.TryParseButtonBet("12.50", out decimal bet);

            Assert.True(parsed);
            Assert.Equal(12.50m, bet);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("12,50")]
    [InlineData("1,000")]
    [InlineData("not-a-number")]
    public void TryParseButtonBet_RejectsAmbiguousValues(string value)
    {
        Assert.False(SlotsModule.TryParseButtonBet(value, out _));
    }
}