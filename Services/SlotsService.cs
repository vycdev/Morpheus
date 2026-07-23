namespace Morpheus.Services;

public sealed class SlotsService
{
    public const decimal TaxRate = 0.05m;
    public const decimal MinBet = 1.00m;
    public const decimal MaxBet = 10000.00m;
    public const int RollRange = 1_000_000;

    private static readonly SlotsOutcome[] Outcomes =
    [
        new(SlotsOutcomeKind.GrandJackpot, "Grand Jackpot \U0001F48E", 1, 500m, true),
        new(SlotsOutcomeKind.MajorJackpot, "Major Jackpot \U0001F451", 5, 100m, true),
        new(SlotsOutcomeKind.MiniJackpot, "Mini Jackpot \U0001F3C6", 20, 50m, true),
        new(SlotsOutcomeKind.MegaWin, "Mega Win \U0001F514", 100, 10m, false),
        new(SlotsOutcomeKind.BigWin, "Big Win \U0001F353", 500, 5m, false),
        new(SlotsOutcomeKind.MediumWin, "Medium Win \U0001F34B", 2500, 3m, false),
        new(SlotsOutcomeKind.SmallWin, "Small Win \U0001F34A", 12000, 2m, false),
        new(SlotsOutcomeKind.TinyWin, "Tiny Win \U0001F347", 80000, 1.5m, false),
        new(SlotsOutcomeKind.Push, "Push \U0001F352", 142857, 1m, false),
        new(SlotsOutcomeKind.Loss, "Loss \U0001F4A9", 762017, 0m, false)
    ];

    public SlotsSpinResult Spin(decimal bet, decimal vaultAmount, int roll)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "Bet must be positive.");

        SlotsOutcome selectedOutcome = SelectOutcome(roll);
        SlotsOutcome originalOutcome = selectedOutcome;
        string resultDescription = selectedOutcome.Name;
        bool jackpotDisabled = false;
        bool payoutCapped = false;

        if (selectedOutcome.IsJackpot && MaxBet * selectedOutcome.Multiplier > vaultAmount)
        {
            selectedOutcome = Outcomes[(int)SlotsOutcomeKind.MegaWin];
            resultDescription = $"~~{originalOutcome.Name}~~ (Vault too low!) -> {selectedOutcome.Name}";
            jackpotDisabled = true;
        }

        decimal grossWinnings;
        if (!jackpotDisabled && selectedOutcome.IsJackpot)
        {
            decimal baseWin = bet * selectedOutcome.Multiplier;
            decimal bonusWin = vaultAmount * GetJackpotBonusPercent(selectedOutcome.Kind) * (bet / MaxBet);
            decimal capAmount = vaultAmount * GetJackpotCapPercent(selectedOutcome.Kind);

            grossWinnings = baseWin + bonusWin;
            if (grossWinnings > capAmount)
            {
                grossWinnings = capAmount;
                resultDescription += " (Capped)";
                payoutCapped = true;
            }
        }
        else
        {
            grossWinnings = bet * selectedOutcome.Multiplier;
        }

        decimal availablePayout = Math.Max(0m, vaultAmount + bet);
        if (grossWinnings > availablePayout)
        {
            grossWinnings = availablePayout;
            resultDescription += " (Capped)";
            payoutCapped = true;
        }

        decimal profit = grossWinnings - bet;
        decimal tax = profit > 0 ? profit * TaxRate : 0m;
        decimal netWinnings = grossWinnings - tax;
        decimal vaultDelta = bet - grossWinnings;

        return new SlotsSpinResult(
            selectedOutcome,
            originalOutcome,
            resultDescription,
            bet,
            grossWinnings,
            tax,
            netWinnings,
            vaultDelta,
            jackpotDisabled,
            payoutCapped);
    }

    public SlotsJackpotPreview CalculateJackpotPreview(decimal bet, decimal vaultAmount) =>
        new(
            CalculateJackpotValue(Outcomes[(int)SlotsOutcomeKind.GrandJackpot], bet, vaultAmount),
            CalculateJackpotValue(Outcomes[(int)SlotsOutcomeKind.MajorJackpot], bet, vaultAmount),
            CalculateJackpotValue(Outcomes[(int)SlotsOutcomeKind.MiniJackpot], bet, vaultAmount));

    internal static SlotsOutcome SelectOutcome(int roll)
    {
        if (roll < 0 || roll >= RollRange)
            throw new ArgumentOutOfRangeException(nameof(roll), $"Roll must be between 0 and {RollRange - 1}.");

        int cumulative = 0;
        foreach (SlotsOutcome outcome in Outcomes)
        {
            cumulative += outcome.Weight;
            if (roll < cumulative)
                return outcome;
        }

        return Outcomes[^1];
    }

    internal static int TotalOutcomeWeight => Outcomes.Sum(outcome => outcome.Weight);

    private static decimal CalculateJackpotValue(SlotsOutcome outcome, decimal bet, decimal vaultAmount)
    {
        if (MaxBet * outcome.Multiplier > vaultAmount)
            return 0m;

        decimal value = bet * outcome.Multiplier + vaultAmount * GetJackpotBonusPercent(outcome.Kind) * (bet / MaxBet);
        decimal cap = vaultAmount * GetJackpotCapPercent(outcome.Kind);

        return value > cap ? cap : value;
    }

    private static decimal GetJackpotBonusPercent(SlotsOutcomeKind kind) =>
        kind switch
        {
            SlotsOutcomeKind.GrandJackpot => 0.25m,
            SlotsOutcomeKind.MajorJackpot => 0.15m,
            SlotsOutcomeKind.MiniJackpot => 0.05m,
            _ => 0m
        };

    private static decimal GetJackpotCapPercent(SlotsOutcomeKind kind) =>
        kind switch
        {
            SlotsOutcomeKind.GrandJackpot => 0.50m,
            SlotsOutcomeKind.MajorJackpot => 0.25m,
            SlotsOutcomeKind.MiniJackpot => 0.10m,
            _ => 1m
        };
}

public enum SlotsOutcomeKind
{
    GrandJackpot = 0,
    MajorJackpot = 1,
    MiniJackpot = 2,
    MegaWin = 3,
    BigWin = 4,
    MediumWin = 5,
    SmallWin = 6,
    TinyWin = 7,
    Push = 8,
    Loss = 9
}

public sealed record SlotsOutcome(
    SlotsOutcomeKind Kind,
    string Name,
    int Weight,
    decimal Multiplier,
    bool IsJackpot);

public sealed record SlotsSpinResult(
    SlotsOutcome Outcome,
    SlotsOutcome OriginalOutcome,
    string ResultDescription,
    decimal Bet,
    decimal GrossWinnings,
    decimal Tax,
    decimal NetWinnings,
    decimal VaultDelta,
    bool IsJackpotDisabled,
    bool IsPayoutCapped);

public sealed record SlotsJackpotPreview(decimal GrandPot, decimal MajorPot, decimal MiniPot);
