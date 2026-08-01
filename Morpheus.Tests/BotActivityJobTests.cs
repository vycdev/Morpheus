using Morpheus.Jobs;

namespace Morpheus.Tests;

public class BotActivityJobTests
{
    private const string PrideActivity = "chanting LGBTQ+ anthems 🌈";

    [Theory]
    [InlineData(6, 1, 0, 0)]
    [InlineData(6, 15, 12, 30)]
    [InlineData(6, 30, 23, 59)]
    public void GetAnnualActivityDescription_DuringJune_ReturnsPrideActivity(
        int month,
        int day,
        int hour,
        int minute)
    {
        DateTime now = new(2026, month, day, hour, minute, 0);

        string? result = BotActivityJob.GetAnnualActivityDescription(now);

        Assert.Equal(PrideActivity, result);
    }

    [Fact]
    public void GetAnnualActivityDescription_OnJulyFirst_ReturnsJulyActivity()
    {
        DateTime now = new(2026, 7, 1, 0, 0, 0);

        string? result = BotActivityJob.GetAnnualActivityDescription(now);

        Assert.Equal("vibing with summer vibes ☀️", result);
    }
}
