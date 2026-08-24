using Discord.Commands;
using Morpheus.Modules;
using System.Reflection;

namespace Morpheus.Tests;

public class HelpCommandRegistrationTests
{
    [Theory]
    [InlineData("2_StocksModule", 2, "StocksModule")]
    [InlineData("10_Misc", 10, "Misc")]
    public void TryParseHelpModuleName_ParsesPageAndModule(string input, int expectedPage, string expectedModule)
    {
        Assert.True(HelpModule.TryParseHelpModuleName(input, out int page, out string module));
        Assert.Equal(expectedPage, page);
        Assert.Equal(expectedModule, module);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("0_StocksModule")]
    [InlineData("-1_StocksModule")]
    [InlineData("2_")]
    public void TryParseHelpModuleName_RejectsMalformedSelections(string input)
    {
        Assert.False(HelpModule.TryParseHelpModuleName(input, out _, out _));
    }

    [Fact]
    public void TryParseHelpModuleName_RejectsAbsentSelection()
    {
        Assert.False(HelpModule.TryParseHelpModuleName(null, out _, out _));
    }

    [Theory]
    [InlineData(1, 11, true, 0, 10)]
    [InlineData(2, 11, true, 10, 11)]
    [InlineData(3, 11, false, 0, 0)]
    [InlineData(int.MaxValue, 11, false, 0, 0)]
    public void TryGetHelpPageBounds_ValidatesActualPageRange(
        int page,
        int visibleCommandCount,
        bool expectedResult,
        int expectedStart,
        int expectedEnd)
    {
        bool result = HelpModule.TryGetHelpPageBounds(page, visibleCommandCount, out int start, out int end);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedStart, start);
        Assert.Equal(expectedEnd, end);
    }

    [Fact]
    public void HelpCommand_AcceptsMultiWordCommandNames()
    {
        MethodInfo method = Assert.Single(
            typeof(HelpModule).GetMethods(),
            method => method.GetCustomAttributes<CommandAttribute>()
                .Any(attribute => attribute.Text == "help"));
        System.Reflection.ParameterInfo parameter = Assert.Single(method.GetParameters());

        Assert.NotNull(parameter.GetCustomAttribute<RemainderAttribute>());
        Assert.True(parameter.HasDefaultValue);
        Assert.Null(parameter.DefaultValue);
    }
}