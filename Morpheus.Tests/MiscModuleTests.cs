using Morpheus.Modules;

namespace Morpheus.Tests;

public class MiscModuleTests
{
    [Theory]
    [InlineData("ROCK", "rock")]
    [InlineData("PaPeR", "paper")]
    [InlineData("ScIsSoRs", "scissors")]
    public void NormalizeRockPaperScissorsChoice_NormalizesMixedCase(string choice, string expected)
    {
        Assert.Equal(expected, MiscModule.NormalizeRockPaperScissorsChoice(choice));
    }
}