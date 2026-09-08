using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Database;
using Morpheus.Handlers;
using Morpheus.Modules;
using System.Reflection;

namespace Morpheus.Tests;

public class HelpCommandRegistrationTests
{
    private const string LongUnicodeSummary =
        "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx😀tail";

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

    [Fact]
    public async Task ModuleHelp_DoesNotSplitSurrogatePairsWhenTruncatingSummaries()
    {
        CommandService commands = new();
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await commands.AddModuleAsync<UnicodeSummaryModule>(services);

        using DiscordSocketClient client = new();
        await using DB db = new(
            new DbContextOptionsBuilder<DB>()
                .UseSqlite("Data Source=:memory:")
                .Options);
        HelpModule module = new(client, commands, new InteractionsHandler(client), services, db);
        MethodInfo createEmbed = typeof(HelpModule).GetMethod(
            "CreateModuleHelpEmbed",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        Embed embed = (Embed)createEmbed.Invoke(module, ["1_UnicodeSummaryModule", "!"])!;
        string fieldValue = Assert.Single(embed.Fields).Value;

        Assert.False(HasUnpairedSurrogate(fieldValue));
    }

    private static bool HasUnpairedSurrogate(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[++i]))
                    return true;
            }
            else if (char.IsLowSurrogate(value[i]))
            {
                return true;
            }
        }

        return false;
    }

    [Name("UnicodeSummaryModule")]
    public class UnicodeSummaryModule : ModuleBase<SocketCommandContext>
    {
        [Command("unicode")]
        [Summary(LongUnicodeSummary)]
        public Task Unicode() => Task.CompletedTask;
    }
}