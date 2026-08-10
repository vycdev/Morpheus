using Morpheus.Services;

namespace Morpheus.Tests;

public class ActivityLevelServiceTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(998, 0)]
    [InlineData(999, 1)]
    [InlineData(1000, 1)]
    public void CalculateLevel_ReturnsExpectedBoundaryLevels(long xp, int expectedLevel)
    {
        Assert.Equal(expectedLevel, ActivityLevelService.CalculateLevel(xp));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(100)]
    public void CalculateXp_ReturnsMinimumXpForLevel(int level)
    {
        int threshold = ActivityLevelService.CalculateXp(level);

        Assert.Equal(level, ActivityLevelService.CalculateLevel(threshold));
        Assert.True(ActivityLevelService.CalculateLevel(threshold - 1) < level);
    }

    [Fact]
    public void CalculateXp_IncreasesWithLevel()
    {
        int previous = ActivityLevelService.CalculateXp(0);

        for (int level = 1; level <= 100; level++)
        {
            int current = ActivityLevelService.CalculateXp(level);

            Assert.True(current > previous);
            previous = current;
        }
    }

    [Fact]
    public void CalculateXp_ThrowsWhenLevelRequiresMoreThanIntMaxXp()
    {
        int highestRepresentableLevel = ActivityLevelService.CalculateLevel(int.MaxValue);

        Assert.Throws<OverflowException>(() => ActivityLevelService.CalculateXp(highestRepresentableLevel + 1));
    }

    [Fact]
    public void CalculateXpLong_ReturnsThresholdAboveIntMaxValue()
    {
        int level = ActivityLevelService.CalculateLevel((long)int.MaxValue + 1) + 1;

        long threshold = ActivityLevelService.CalculateXpLong(level);

        Assert.True(threshold > int.MaxValue);
        Assert.Equal(level, ActivityLevelService.CalculateLevel(threshold));
        Assert.True(ActivityLevelService.CalculateLevel(threshold - 1) < level);
    }

    [Fact]
    public void CalculateXpLong_HandlesHighestRepresentableLongLevel()
    {
        int highestRepresentableLevel = ActivityLevelService.CalculateLevel(long.MaxValue);

        long threshold = ActivityLevelService.CalculateXpLong(highestRepresentableLevel);

        Assert.Equal(highestRepresentableLevel, ActivityLevelService.CalculateLevel(threshold));
        Assert.True(ActivityLevelService.CalculateLevel(threshold - 1) < highestRepresentableLevel);
        Assert.Throws<OverflowException>(() => ActivityLevelService.CalculateXpLong(highestRepresentableLevel + 1));
    }
}
