using Morpheus.Services;

namespace Morpheus.Tests;

public class SlotsServiceTests
{
    private readonly SlotsService slotsService = new();

    [Fact]
    public void OutcomeWeights_CoverEntireRollRange()
    {
        Assert.Equal(SlotsService.RollRange, SlotsService.TotalOutcomeWeight);
    }

    [Theory]
    [InlineData(0, SlotsOutcomeKind.GrandJackpot)]
    [InlineData(1, SlotsOutcomeKind.MajorJackpot)]
    [InlineData(5, SlotsOutcomeKind.MajorJackpot)]
    [InlineData(6, SlotsOutcomeKind.MiniJackpot)]
    [InlineData(25, SlotsOutcomeKind.MiniJackpot)]
    [InlineData(26, SlotsOutcomeKind.MegaWin)]
    [InlineData(95_126, SlotsOutcomeKind.Push)]
    [InlineData(237_983, SlotsOutcomeKind.Loss)]
    [InlineData(999_999, SlotsOutcomeKind.Loss)]
    public void SelectOutcome_UsesWeightedBoundaries(int roll, SlotsOutcomeKind expectedOutcome)
    {
        SlotsOutcome outcome = SlotsService.SelectOutcome(roll);

        Assert.Equal(expectedOutcome, outcome.Kind);
    }

    [Fact]
    public void Spin_DisabledJackpotDowngradesToMegaWin()
    {
        SlotsSpinResult result = slotsService.Spin(100m, 1_000m, roll: 0);

        Assert.True(result.IsJackpotDisabled);
        Assert.False(result.IsPayoutCapped);
        Assert.Equal(SlotsOutcomeKind.GrandJackpot, result.OriginalOutcome.Kind);
        Assert.Equal(SlotsOutcomeKind.MegaWin, result.Outcome.Kind);
        Assert.Equal(1_000m, result.GrossWinnings);
        Assert.Equal(45m, result.Tax);
        Assert.Equal(955m, result.NetWinnings);
        Assert.Equal(-900m, result.VaultDelta);
        Assert.Contains("Vault too low", result.ResultDescription);
    }

    [Fact]
    public void Spin_GrandJackpotAppliesPayoutCap()
    {
        SlotsSpinResult result = slotsService.Spin(10_000m, 10_000_000m, roll: 0);

        Assert.True(result.IsPayoutCapped);
        Assert.False(result.IsJackpotDisabled);
        Assert.Equal(SlotsOutcomeKind.GrandJackpot, result.Outcome.Kind);
        Assert.Equal(5_000_000m, result.GrossWinnings);
        Assert.Equal(249_500m, result.Tax);
        Assert.Equal(4_750_500m, result.NetWinnings);
        Assert.Equal(-4_990_000m, result.VaultDelta);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(26, false)]
    public void Spin_UnderfundedWinCannotOverdrawVault(int roll, bool jackpotDisabled)
    {
        const decimal vaultAmount = 1_000m;

        SlotsSpinResult result = slotsService.Spin(10_000m, vaultAmount, roll);

        Assert.Equal(jackpotDisabled, result.IsJackpotDisabled);
        Assert.Equal(SlotsOutcomeKind.MegaWin, result.Outcome.Kind);
        Assert.True(result.IsPayoutCapped);
        Assert.Equal(11_000m, result.GrossWinnings);
        Assert.Equal(-vaultAmount, result.VaultDelta);
        Assert.Equal(0m, vaultAmount + result.VaultDelta);
    }

    [Fact]
    public void Spin_PushReturnsBetWithoutTaxOrVaultMovement()
    {
        SlotsSpinResult result = slotsService.Spin(100m, 10_000m, roll: 95_126);

        Assert.Equal(SlotsOutcomeKind.Push, result.Outcome.Kind);
        Assert.Equal(100m, result.GrossWinnings);
        Assert.Equal(0m, result.Tax);
        Assert.Equal(100m, result.NetWinnings);
        Assert.Equal(0m, result.VaultDelta);
    }

    [Fact]
    public void Spin_LossMovesBetIntoVault()
    {
        SlotsSpinResult result = slotsService.Spin(50m, 10_000m, roll: 999_999);

        Assert.Equal(SlotsOutcomeKind.Loss, result.Outcome.Kind);
        Assert.Equal(0m, result.GrossWinnings);
        Assert.Equal(0m, result.Tax);
        Assert.Equal(0m, result.NetWinnings);
        Assert.Equal(50m, result.VaultDelta);
    }

    [Fact]
    public void CalculateJackpotPreview_ReturnsZeroWhenVaultCannotCoverMaxLiability()
    {
        SlotsJackpotPreview preview = slotsService.CalculateJackpotPreview(100m, 1_000m);

        Assert.Equal(0m, preview.GrandPot);
        Assert.Equal(0m, preview.MajorPot);
        Assert.Equal(0m, preview.MiniPot);
    }

    [Fact]
    public void CalculateJackpotPreview_AppliesJackpotCaps()
    {
        SlotsJackpotPreview preview = slotsService.CalculateJackpotPreview(10_000m, 10_000_000m);

        Assert.Equal(5_000_000m, preview.GrandPot);
        Assert.Equal(2_500_000m, preview.MajorPot);
        Assert.Equal(1_000_000m, preview.MiniPot);
    }
}
