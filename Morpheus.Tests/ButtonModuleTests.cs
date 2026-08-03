using Morpheus.Modules;

namespace Morpheus.Tests;

public class ButtonModuleTests
{
    [Theory]
    [InlineData(10L, 100L, false)]
    [InlineData(101L, 100L, true)]
    [InlineData(100L, 100L, false)]
    [InlineData(1L, null, false)]
    public void IsNewBestScore_ComparesAgainstHighestPreviousScore(
        long score,
        long? bestScore,
        bool expected)
    {
        Assert.Equal(expected, ButtonModule.IsNewBestScore(score, bestScore));
    }
}
