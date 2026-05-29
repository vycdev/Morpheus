using System.Globalization;
using Morpheus.Services;

namespace Morpheus.Tests;

public class EconomyServiceTests
{
    [Fact]
    public void OrderUserIdsForUpdate_DeduplicatesAndSortsIds()
    {
        int[] orderedIds = [.. EconomyService.OrderUserIdsForUpdate([42, 7, 42, 1])];

        Assert.Equal([1, 7, 42], orderedIds);
    }

    [Fact]
    public void FormatMoneyForStorage_UsesInvariantTwoDecimalPlaces()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            string formatted = EconomyService.FormatMoneyForStorage(1234.5m);

            Assert.Equal("1234.50", formatted);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ParseMoneyFromStorage_PrefersInvariantCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            decimal parsed = EconomyService.ParseMoneyFromStorage("1,234.50", fallback: 99m);

            Assert.Equal(1234.50m, parsed);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ParseMoneyFromStorage_FallsBackToCurrentCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            decimal parsed = EconomyService.ParseMoneyFromStorage("1234,50", fallback: 99m);

            Assert.Equal(1234.50m, parsed);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ParseMoneyFromStorage_ReturnsFallbackForInvalidValues()
    {
        decimal parsed = EconomyService.ParseMoneyFromStorage("not-money", fallback: 123m);

        Assert.Equal(123m, parsed);
    }
}
