using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.Services;
using Morpheus.Utilities;
using System.Text;

namespace Morpheus.Modules;

[Name("Slots")]
[Summary("Slot machine gambling — spin the reels and test your luck.")]
public class SlotsModule : ModuleBase<SocketCommandContextExtended>
{
    private const decimal TaxRate = 0.05m; // 5% tax on profit
    private const decimal MinBet = 1.00m;
    private const decimal MaxBet = 10000.00m;

    private readonly DB dbContext;
    private readonly EconomyService economyService;

    public SlotsModule(DB dbContext, InteractionsHandler interactionHandler, EconomyService economyService)
    {
        this.dbContext = dbContext;
        this.economyService = economyService;
        interactionHandler.RegisterInteraction("slots_spin", HandleSpinInteraction);
    }

    // ── Reel symbols with weights (higher weight = more common) ──
    private static readonly (string emoji, string name, int weight)[] Symbols =
    [
        ("💎", "Diamond",  2),
        ("👑", "Crown",    3),
        ("🍒", "Cherry",   5),
        ("🔔", "Bell",     6),
        ("⭐", "Star",     7),
        ("🍋", "Lemon",    8),
        ("🍊", "Orange",   9),
        ("🍇", "Grape",   10),
    ];

    // ── Payout table (multiplier of bet) ──
    // Three of a kind
    private static readonly Dictionary<string, decimal> ThreeOfAKindPayouts = new()
    {
        { "💎", 50.0m },   // Jackpot
        { "👑", 25.0m },
        { "🍒", 15.0m },
        { "🔔", 10.0m },
        { "⭐",  8.0m },
        { "🍋",  5.0m },
        { "🍊",  4.0m },
        { "🍇",  3.0m },
    };

    // Two of a kind (any position)
    private static readonly Dictionary<string, decimal> TwoOfAKindPayouts = new()
    {
        { "💎", 5.0m },
        { "👑", 3.0m },
        { "🍒", 2.0m },
    };

    /// <summary>
    /// Spins one reel using weighted random selection.
    /// </summary>
    private static string SpinReel()
    {
        int totalWeight = 0;
        foreach (var s in Symbols) totalWeight += s.weight;

        int roll = Random.Shared.Next(totalWeight);
        int cumulative = 0;
        foreach (var (emoji, _, weight) in Symbols)
        {
            cumulative += weight;
            if (roll < cumulative)
                return emoji;
        }
        return Symbols[^1].emoji; // fallback
    }

    /// <summary>
    /// Calculates the payout multiplier for a given spin result.
    /// </summary>
    private static (decimal multiplier, string description) CalculatePayout(string r1, string r2, string r3)
    {
        // Three of a kind
        if (r1 == r2 && r2 == r3)
        {
            if (ThreeOfAKindPayouts.TryGetValue(r1, out decimal payout))
                return (payout, r1 == "💎" ? "💎💎💎 JACKPOT!" : $"Three {r1} — Big win!");
            return (2.0m, $"Three {r1}!");
        }

        // Two of a kind — check all pair positions
        string? pairSymbol = null;
        if (r1 == r2) pairSymbol = r1;
        else if (r1 == r3) pairSymbol = r1;
        else if (r2 == r3) pairSymbol = r2;

        if (pairSymbol != null && TwoOfAKindPayouts.TryGetValue(pairSymbol, out decimal twoPayout))
            return (twoPayout, $"Two {pairSymbol} — Nice!");

        // Special combo: Cherry anywhere = 1x (get your money back)
        if (r1 == "🍒" || r2 == "🍒" || r3 == "🍒")
            return (1.0m, "🍒 Cherry saves you!");

        // No win
        return (0m, "No match — better luck next time!");
    }

    /// <summary>
    /// Builds the visual slot machine display using emoji (no code block).
    /// </summary>
    private static string BuildSlotMachine(string r1, string r2, string r3)
    {
        StringBuilder sb = new();
        sb.AppendLine("⬛⬛⬛⬛⬛⬛⬛⬛");
        sb.AppendLine($"⬛ {r1} ▪ {r2} ▪ {r3} ⬛");
        sb.AppendLine("⬛⬛⬛⬛⬛⬛⬛⬛");
        return sb.ToString();
    }

    /// <summary>
    /// Core spin logic used by both the command and the button interaction.
    /// Returns (embed, bet) for building the response.
    /// </summary>
    private async Task<(Embed embed, decimal bet, ComponentBuilder components)?> ExecuteSpin(int userId, decimal bet)
    {
        User? user = await dbContext.Users.FindAsync(userId);
        if (user == null) return null;

        // Validate bet
        if (bet < MinBet || bet > MaxBet) return null;
        if (user.Balance < bet) return null;

        // Deduct bet (tax is only taken from profits)
        user.Balance -= bet;

        // Spin the reels
        string r1 = SpinReel();
        string r2 = SpinReel();
        string r3 = SpinReel();

        var (multiplier, resultDescription) = CalculatePayout(r1, r2, r3);

        decimal grossWinnings = bet * multiplier;
        bool isWin = multiplier > 0m;
        decimal profit = grossWinnings - bet;
        decimal tax = 0m;

        if (profit > 0)
        {
            tax = profit * TaxRate;
        }

        // Add tax to UBI pool
        await economyService.AddToPool(tax);

        decimal netWinnings = grossWinnings - tax;
        decimal finalProfitOrLoss = netWinnings - bet;

        if (isWin)
        {
            user.Balance += netWinnings;
        }

        // Record transaction
        StockTransaction transaction = new()
        {
            UserId = user.Id,
            Type = isWin ? TransactionType.SlotsWin : TransactionType.SlotsLoss,
            Amount = isWin ? netWinnings : bet,
            Fee = tax,
            InsertDate = DateTime.UtcNow
        };
        await dbContext.StockTransactions.AddAsync(transaction);
        await dbContext.SaveChangesAsync();

        // Build embed
        string slotDisplay = BuildSlotMachine(r1, r2, r3);
        Color embedColor = multiplier >= 10m ? new Color(255, 215, 0) // gold for big wins
                         : isWin ? Color.Green
                         : Color.Red;

        string title = multiplier >= 50m ? "🎰 JACKPOT!!!"
                     : multiplier >= 10m ? "🎰 BIG WIN!"
                     : isWin && finalProfitOrLoss > 0 ? "🎰 Winner!"
                     : isWin ? "🎰 Push"
                     : "🎰 No Luck";

        StringBuilder desc = new();
        desc.AppendLine(slotDisplay);
        desc.AppendLine($"**{resultDescription}**");
        desc.AppendLine();

        if (isWin)
        {
            desc.AppendLine($"Payout: **{multiplier}x** of **${bet:F2}** = **${grossWinnings:F2}**");

            if (tax > 0)
            {
                desc.AppendLine($"Tax (5% on profit): -**${tax:F2}**");
                desc.AppendLine($"Net Payout: **${netWinnings:F2}**");
            }

            if (finalProfitOrLoss > 0)
                desc.AppendLine($"Profit: +**${finalProfitOrLoss:F2}** 📈");
            else if (finalProfitOrLoss == 0)
                desc.AppendLine($"Break even (Money back)");
            else
                desc.AppendLine($"Net loss: -**${Math.Abs(finalProfitOrLoss):F2}** 📉");
        }
        else
        {
            desc.AppendLine($"You lost **${bet:F2}** 💸");
        }

        desc.AppendLine($"\nBalance: **${user.Balance:F2}**");

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(desc.ToString())
            .WithColor(embedColor)
            .WithFooter($"Bet: ${bet:F2} | {user.Username}")
            .WithCurrentTimestamp();

        // Build buttons: Spin Again (same bet), Double, Half
        decimal doubleBet = bet * 2;
        decimal halfBet = Math.Floor(bet / 2 * 100) / 100;
        bool canDouble = doubleBet <= MaxBet;
        bool canHalf = halfBet >= MinBet;

        ComponentBuilder components = new ComponentBuilder()
            .WithButton($"Spin Again (${bet:F2})", customId: $"slots_again:{userId}:{bet:F2}", style: ButtonStyle.Primary, emote: new Emoji("🔁"))
            .WithButton($"Double (${(canDouble ? doubleBet : bet):F2})", customId: $"slots_double:{userId}:{doubleBet:F2}", style: ButtonStyle.Danger, emote: new Emoji("⏫"), disabled: !canDouble)
            .WithButton($"Half (${(canHalf ? halfBet : bet):F2})", customId: $"slots_half:{userId}:{halfBet:F2}", style: ButtonStyle.Secondary, emote: new Emoji("⏬"), disabled: !canHalf);

        return (embed.Build(), bet, components);
    }

    [Name("Slots")]
    [Summary("Spin the slot machine! Bet an amount and try your luck. 5% tax is always deducted from your bet.")]
    [Command("slots")]
    [Alias("slot", "spin")]
    [RateLimit(3, 15)]
    public async Task SlotsAsync(decimal bet)
    {
        User? user = Context.DbUser;
        if (user == null) { await ReplyAsync("User not found."); return; }

        if (bet < MinBet)
        {
            await ReplyAsync($"Minimum bet is **${MinBet:F2}**.");
            return;
        }
        if (bet > MaxBet)
        {
            await ReplyAsync($"Maximum bet is **${MaxBet:F2}**.");
            return;
        }
        if (user.Balance < bet)
        {
            await ReplyAsync($"Insufficient balance. You have **${user.Balance:F2}**.");
            return;
        }

        var result = await ExecuteSpin(user.Id, bet);
        if (result == null)
        {
            await ReplyAsync("Something went wrong.");
            return;
        }

        var (embed, _, components) = result.Value;
        await ReplyAsync(embed: embed, components: components.Build());
    }

    /// <summary>
    /// Handles the Spin Again / Double / Half button interactions.
    /// </summary>
    private async Task HandleSpinInteraction(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent comp) return;

        string custom = comp.Data.CustomId ?? string.Empty;
        if (!custom.StartsWith("slots_again:") && !custom.StartsWith("slots_double:") && !custom.StartsWith("slots_half:"))
            return;

        // Format: slots_{action}:{userId}:{bet}
        string[] parts = custom.Split(':', 3);
        if (parts.Length < 3) return;
        if (!int.TryParse(parts[1], out int userId)) return;
        if (!decimal.TryParse(parts[2], out decimal bet)) return;

        // Only the original user can use the buttons
        User? user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || user.DiscordId != comp.User.Id)
        {
            try { await comp.RespondAsync("These aren't your slots!", ephemeral: true); } catch { }
            return;
        }

        // Re-fetch as tracking for balance update
        user = await dbContext.Users.FindAsync(userId);
        if (user == null) return;

        if (user.Balance < bet)
        {
            try { await comp.RespondAsync($"Insufficient balance. You have **${user.Balance:F2}**.", ephemeral: true); } catch { }
            return;
        }

        var result = await ExecuteSpin(userId, bet);
        if (result == null)
        {
            try { await comp.RespondAsync("Something went wrong.", ephemeral: true); } catch { }
            return;
        }

        var (embed, _, components) = result.Value;

        // Disable buttons on the old message
        try
        {
            await comp.UpdateAsync(msg =>
            {
                msg.Components = new ComponentBuilder().Build(); // clear old buttons
            });
        }
        catch { }

        // Send new spin as a new message with fresh buttons
        await comp.Channel.SendMessageAsync(embed: embed, components: components.Build());
    }

    [Name("Slots Paytable")]
    [Summary("View the slot machine payout table and symbol odds.")]
    [Command("slots paytable")]
    [Alias("slots help", "slots info", "paytable", "slothelp")]
    [RateLimit(2, 10)]
    public async Task SlotsPaytableAsync()
    {
        StringBuilder sb = new();

        sb.AppendLine("**Three of a Kind:**");
        foreach (var (emoji, payout) in ThreeOfAKindPayouts)
            sb.AppendLine($"  {emoji}{emoji}{emoji} — **{payout}x**{(payout >= 50 ? " 💎 JACKPOT" : "")}");

        sb.AppendLine();
        sb.AppendLine("**Two of a Kind (any position):**");
        foreach (var (emoji, payout) in TwoOfAKindPayouts)
            sb.AppendLine($"  {emoji}{emoji} — **{payout}x**");

        sb.AppendLine();
        sb.AppendLine("**Special:**");
        sb.AppendLine("  🍒 anywhere — **1x** (get your bet back)");

        sb.AppendLine();
        sb.AppendLine("**Rules:**");
        sb.AppendLine($"  Min bet: **${MinBet:F2}** | Max bet: **${MaxBet:F2}**");
        sb.AppendLine("  5% tax is deducted from your profits only");
        sb.AppendLine("  Payouts are based on the full bet amount");

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle("🎰 Slots Paytable")
            .WithDescription(sb.ToString())
            .WithColor(Colors.Blue)
            .WithCurrentTimestamp();

        await ReplyAsync(embed: embed.Build());
    }
}
