using System.Globalization;
using System.Runtime.CompilerServices;
using Discord.Commands;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Modules;

namespace Morpheus.Tests;

public class StocksModuleTests
{
    [SupportedCultureTheory("tr-TR")]
    [InlineData("GUILD")]
    public async Task ResolveTarget_AcceptsGuildKeywordUnderTurkishCulture(string target)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var module = new StocksModule(null!, null!, null!, null!);
            SocketCommandContextExtended context =
                (SocketCommandContextExtended)RuntimeHelpers.GetUninitializedObject(
                    typeof(SocketCommandContextExtended));
            context.DbGuild = new Guild { Id = 42, DiscordId = 1, Name = "Test guild" };
            ((IModuleBase)module).SetContext(context);
            var result = await module.ResolveTarget(target);

            Assert.NotNull(result);
            Assert.Equal(StockEntityType.Guild, result.Value.type);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

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
