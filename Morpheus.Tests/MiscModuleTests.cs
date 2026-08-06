using System.Globalization;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Database;
using Morpheus.Modules;

namespace Morpheus.Tests;

public class MiscModuleTests
{
    [Theory]
    [InlineData(2026, 12, 24, 12, 12, 25, 2026)]
    [InlineData(2026, 12, 25, 0, 12, 25, 2026)]
    [InlineData(2026, 12, 25, 12, 12, 25, 2027)]
    [InlineData(2026, 11, 1, 12, 10, 31, 2027)]
    [InlineData(2026, 9, 3, 12, 9, 2, 2027)]
    public void GetNextAnnualEventDate_ReturnsNextOccurrence(
        int currentYear,
        int currentMonth,
        int currentDay,
        int currentHour,
        int eventMonth,
        int eventDay,
        int expectedYear)
    {
        DateTime now = new(currentYear, currentMonth, currentDay, currentHour, 0, 0, DateTimeKind.Utc);

        DateTime result = MiscModule.GetNextAnnualEventDate(now, eventMonth, eventDay);

        Assert.Equal(new DateTime(expectedYear, eventMonth, eventDay, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Theory]
    [InlineData("CHRISTMAS", "christmas")]
    [InlineData("CC BIRTHDAY", "cc birthday")]
    public void NormalizeTimeUntilEventName_NormalizesCase(string eventName, string expected)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            Assert.Equal(expected, MiscModule.NormalizeTimeUntilEventName(eventName));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("ROCK", "rock")]
    [InlineData("PaPeR", "paper")]
    [InlineData("ScIsSoRs", "scissors")]
    public void NormalizeRockPaperScissorsChoice_NormalizesMixedCase(string choice, string expected)
    {
        Assert.Equal(expected, MiscModule.NormalizeRockPaperScissorsChoice(choice));
    }

    [Fact]
    public void BuildInfoFooter_DoesNotDependOnOwnerConfiguration()
    {
        Discord.EmbedFooterBuilder footer = MiscModule.BuildInfoFooter();

        Assert.Equal("Made with ❤️ by vycdev", footer.Text);
        Assert.Null(footer.IconUrl);
    }

    [Fact]
    public void ParseChoices_IgnoresBlankLinesAndTrimsOptions()
    {
        string[] result = MiscModule.ParseChoices("  red  \n\n \t\nblue\r\n");

        Assert.Equal(["red", "blue"], result);
    }

    [Fact]
    public void ParseChoices_ReturnsOnlyNonBlankOptions()
    {
        string[] result = MiscModule.ParseChoices("\n \n only choice \n\t");

        Assert.Equal(["only choice"], result);
    }

    [Fact]
    public void GenerateRandomNumber_SupportsIntMaxValueAsInclusiveUpperBound()
    {
        int result = MiscModule.GenerateRandomNumber(int.MaxValue, int.MaxValue);

        Assert.Equal(int.MaxValue, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildUrbanDictionaryUrl_UsesRandomEndpointForMissingTerms(string? word)
    {
        Assert.Equal("https://api.urbandictionary.com/v0/random", MiscModule.BuildUrbanDictionaryUrl(word));
    }

    [Fact]
    public void BuildUrbanDictionaryUrl_EncodesReservedQueryCharacters()
    {
        string result = MiscModule.BuildUrbanDictionaryUrl("C# & tea");

        Assert.Equal("https://api.urbandictionary.com/v0/define?term=C%23%20%26%20tea", result);
    }

    [Fact]
    public async Task LoveCompatibilityCommand_AllowsComparingWithInvoker()
    {
        CommandService commands = new();
        await using DB db = new(
            new DbContextOptionsBuilder<DB>()
                .UseSqlite("Data Source=:memory:")
                .Options);
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(commands)
            .AddSingleton(db)
            .BuildServiceProvider();
        await commands.AddModuleAsync<MiscModule>(services);
        CommandInfo command = Assert.Single(
            commands.Commands,
            command => command.Aliases.Contains("love"));
        Discord.Commands.ParameterInfo secondUser = command.Parameters[1];

        Assert.True(secondUser.IsOptional);
        Assert.Null(secondUser.DefaultValue);
    }

    [Theory]
    [InlineData("1d6", 1, 6)]
    [InlineData("2D20", 2, 20)]
    public void TryParseDiceInput_AcceptsEitherSeparatorCase(string input, int expectedCount, int expectedSides)
    {
        bool parsed = MiscModule.TryParseDiceInput(input, out int count, out int sides);

        Assert.True(parsed);
        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedSides, sides);
    }

    [Theory]
    [InlineData("1d6D8")]
    [InlineData("1D")]
    [InlineData("D6")]
    [InlineData("0D6")]
    [InlineData("1D1")]
    public void TryParseDiceInput_RejectsMalformedOrOutOfRangeInput(string input)
    {
        Assert.False(MiscModule.TryParseDiceInput(input, out _, out _));
    }
}
