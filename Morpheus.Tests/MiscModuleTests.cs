using System.Globalization;
using System.Reflection;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Database;
using Morpheus.Modules;
using Morpheus.Utilities.Extensions;

namespace Morpheus.Tests;

public sealed class SupportedCultureTheoryAttribute : TheoryAttribute
{
    public SupportedCultureTheoryAttribute(string cultureName)
    {
        try
        {
            CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            Skip = $"The {cultureName} culture is unavailable in globalization-invariant mode.";
        }
    }
}

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

    [SupportedCultureTheory("tr-TR")]
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
    [InlineData("  christmas  ", "christmas")]
    [InlineData("\tcc birthday\r\n", "cc birthday")]
    public void NormalizeTimeUntilEventName_TrimsSurroundingWhitespace(string eventName, string expected)
    {
        Assert.Equal(expected, MiscModule.NormalizeTimeUntilEventName(eventName));
    }

    [Theory]
    [InlineData("ROCK", "rock")]
    [InlineData("PaPeR", "paper")]
    [InlineData(" ScIsSoRs ", "scissors")]
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
    public void ParseChoices_TruncatesOptionsToKeepReplyWithinDiscordLimit()
    {
        string[] result = MiscModule.ParseChoices($"{new string('x', 1986)}\nshort");

        Assert.Equal(2000 - "Hmmm I choose: ".Length, result[0].Length);
        Assert.EndsWith("…", result[0]);
        Assert.Equal("short", result[1]);
    }

    [Fact]
    public void ParseChoices_DoesNotSplitSurrogatePairsWhenTruncating()
    {
        string[] result = MiscModule.ParseChoices(new string('x', 1983) + "😀tail\nshort");

        Assert.Equal(new string('x', 1983) + "…", result[0]);
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

    [Theory]
    [InlineData("data:image/png;base64,AQID")]
    [InlineData("AQID")]
    public void TryDecodeMinecraftFavicon_DecodesSupportedBase64Formats(string favicon)
    {
        bool decoded = MiscModule.TryDecodeMinecraftFavicon(favicon, out byte[] imageBytes);

        Assert.True(decoded);
        Assert.Equal([1, 2, 3], imageBytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("data:image/png,not-base64")]
    [InlineData("data:image/png;base64,")]
    [InlineData("not-base64")]
    public void TryDecodeMinecraftFavicon_RejectsMissingOrMalformedImages(string? favicon)
    {
        bool decoded = MiscModule.TryDecodeMinecraftFavicon(favicon, out byte[] imageBytes);

        Assert.False(decoded);
        Assert.Empty(imageBytes);
    }

    [Fact]
    public void UrbanDictionaryCommand_AcceptsMultiWordTerms()
    {
        MethodInfo method = Assert.Single(
            typeof(MiscModule).GetMethods(),
            method => method.GetCustomAttributes<CommandAttribute>()
                .Any(attribute => attribute.Text == "udic"));
        System.Reflection.ParameterInfo parameter = Assert.Single(method.GetParameters());

        Assert.NotNull(parameter.GetCustomAttribute<RemainderAttribute>());
        Assert.True(parameter.HasDefaultValue);
        Assert.Null(parameter.DefaultValue);
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

    [Theory]
    [InlineData(-1, "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░")]
    [InlineData(101, "██████████████████████████████")]
    public void GetPercentageBar_ClampsOutOfRangeValues(int value, string expected)
    {
        Assert.Equal(expected, value.GetPercentageBar());
    }

    [Fact]
    public void FormatUserRoles_PreservesRoleNamesWithinEmbedFieldLimit()
    {
        string result = MiscModule.FormatUserRoles(["Admin", "Member"]);

        Assert.Equal("Admin, Member", result);
    }

    [Fact]
    public void FormatUserRoles_SummarizesRolesBeyondEmbedFieldLimit()
    {
        string[] roles = Enumerable.Range(1, 250)
            .Select(index => $"role-{index:D3}-{new string('a', 90)}")
            .ToArray();

        string result = MiscModule.FormatUserRoles(roles);

        Assert.InRange(result.Length, 1, Discord.EmbedFieldBuilder.MaxFieldValueLength);
        Assert.StartsWith(roles[0], result);
        Assert.Contains(" more)", result);
        Assert.DoesNotContain(roles[^1], result);
    }

    [Fact]
    public void FormatUserRoles_UsesPlaceholderWhenNoRolesAreAvailable()
    {
        Assert.Equal("None", MiscModule.FormatUserRoles([]));
    }
}
