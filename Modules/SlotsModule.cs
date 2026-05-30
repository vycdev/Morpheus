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
using System.Data;
using System.Text;

namespace Morpheus.Modules;

[Name("Slots")]
[Summary("Slot machine gambling — spin the reels and test your luck.")]
public class SlotsModule : ModuleBase<SocketCommandContextExtended>
{
    private readonly DB dbContext;
    private readonly EconomyService economyService;
    private readonly SlotsService slotsService;

    public SlotsModule(
        DB dbContext,
        InteractionsHandler interactionHandler,
        EconomyService economyService,
        SlotsService slotsService)
    {
        this.dbContext = dbContext;
        this.economyService = economyService;
        this.slotsService = slotsService;
        interactionHandler.RegisterInteraction("slots_spin", HandleSpinInteraction);
    }

    // ── Reel symbols with weights (higher weight = more common) ──
    // Used for visual generation only
    private static readonly string[] VisualSymbols = ["💎", "👑", "🏆", "🔔", "🍓", "🍋", "🍊", "🍇", "💩"];

    /// <summary>
    /// Spins the reels based on a predetermined outcome.
    /// </summary>
    private static (string r1, string r2, string r3) GenerateReels(string outcomeName)
    {
        string symbol;
        if (outcomeName.Contains("Grand")) symbol = "💎";
        else if (outcomeName.Contains("Major")) symbol = "👑";
        else if (outcomeName.Contains("Mini")) symbol = "🏆";
        else if (outcomeName.Contains("Mega")) symbol = "🔔";
        else if (outcomeName.Contains("Big")) symbol = "🍓"; // 3 strawberries
        else if (outcomeName.Contains("Medium")) symbol = "🍋";
        else if (outcomeName.Contains("Small")) symbol = "🍊";
        else if (outcomeName.Contains("Tiny")) return ("🍇", "🍇", Random.Shared.Next(2) == 0 ? "🍒" : "🍋"); // 2 grapes
        else if (outcomeName.Contains("Push")) return ("🍒", "🍋", "🍊"); // Cherry anywhere (visual cheat: put it first)
        else return GenerateLossReels();

        return (symbol, symbol, symbol);
    }

    private static (string r1, string r2, string r3) GenerateLossReels()
    {
        // Generate 3 random symbols that are NOT a winning combo
        // Simple heuristic: ensure they aren't all the same
        string s1 = VisualSymbols[Random.Shared.Next(VisualSymbols.Length)];
        string s2 = VisualSymbols[Random.Shared.Next(VisualSymbols.Length)];
        string s3 = VisualSymbols[Random.Shared.Next(VisualSymbols.Length)];

        while (s1 == s2 && s2 == s3)
        {
            s3 = VisualSymbols[Random.Shared.Next(VisualSymbols.Length)];
        }
        return (s1, s2, s3);
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
        await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        await economyService.LockPoolForUpdate();
        decimal vaultAmount = await economyService.LockVaultForUpdate();

        User? user = await economyService.LockUserForUpdate(userId);
        if (user == null) return null;

        // Validate bet
        if (bet < SlotsService.MinBet || bet > SlotsService.MaxBet) return null;
        if (user.Balance < bet) return null;

        // Deduct bet from user (goes to balance temporarily, will be moved to Vault)
        user.Balance -= bet;

        SlotsSpinResult spin = slotsService.Spin(bet, vaultAmount, Random.Shared.Next(SlotsService.RollRange));

        if (spin.VaultDelta != 0)
            await economyService.UpdateVault(spin.VaultDelta);

        if (spin.Tax > 0)
            await economyService.AddToPool(spin.Tax);

        user.Balance += spin.NetWinnings;

        // Record transaction
        StockTransaction transaction = new()
        {
            UserId = user.Id,
            Type = spin.GrossWinnings > 0 ? TransactionType.SlotsWin : TransactionType.SlotsLoss,
            Amount = spin.NetWinnings,
            Fee = spin.Tax,
            InsertDate = DateTime.UtcNow
        };
        await dbContext.StockTransactions.AddAsync(transaction);
        await dbContext.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        // 4. Build Visuals
        var (r1, r2, r3) = GenerateReels(spin.Outcome.Name);
        string slotDisplay = BuildSlotMachine(r1, r2, r3);
        
        Color embedColor = spin.Outcome.IsJackpot ? new Color(255, 215, 0) // Gold
                         : spin.GrossWinnings > bet ? Color.Green
                         : spin.GrossWinnings == bet ? Color.Blue
                         : Color.Red;

        string title = spin.Outcome.IsJackpot ? "🎰 JACKPOT!!!" : "🎰 Slots";

        // Calculate potential jackpots for THIS bet using CURRENT vault (post-spin)
        // This entices the user for the next spin or shows what they were playing for.
        // Vault has just been updated, so get fresh amount.
        decimal freshVault = await economyService.GetVaultAmount();
        SlotsJackpotPreview jackpotPreview = slotsService.CalculateJackpotPreview(bet, freshVault);

        StringBuilder desc = new();
        desc.AppendLine(slotDisplay);
        
        // Show potential jackpots cleanly
        string pots = "";
        if (jackpotPreview.GrandPot > 0) pots += $"💎 ${jackpotPreview.GrandPot:N0}  ";
        if (jackpotPreview.MajorPot > 0) pots += $"👑 ${jackpotPreview.MajorPot:N0}  ";
        if (jackpotPreview.MiniPot > 0) pots += $"🏆 ${jackpotPreview.MiniPot:N0}";
        
        if (!string.IsNullOrWhiteSpace(pots))
            desc.AppendLine($"*{pots.Trim()}*");

        desc.AppendLine($"**{spin.ResultDescription}**");
        desc.AppendLine();

        if (spin.GrossWinnings > 0)
        {
            desc.AppendLine($"Payout: **${spin.GrossWinnings:F2}**");
            if (spin.Tax > 0) desc.AppendLine($"Tax (5% Profit): -**${spin.Tax:F2}**");
            desc.AppendLine($"Net: **${spin.NetWinnings:F2}**");
        }
        else
        {
            desc.AppendLine($"You lost **${bet:F2}** 💸");
        }

        desc.AppendLine($"\nBalance: **${user.Balance:F2}**");

        // Get fresh vault amount for display
        decimal currentVault = await economyService.GetVaultAmount();
        string footerText = $"Bet: ${bet:F2} | Vault: ${currentVault:N0}";

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(desc.ToString())
            .WithColor(embedColor)
            .WithFooter(footerText)
            .WithCurrentTimestamp();

        // Build buttons: Spin Again (same bet), Double, Half
        decimal doubleBet = bet * 2;
        decimal halfBet = Math.Floor(bet / 2 * 100) / 100;
        bool canDouble = doubleBet <= SlotsService.MaxBet;
        bool canHalf = halfBet >= SlotsService.MinBet;

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

        if (bet < SlotsService.MinBet)
        {
            await ReplyAsync($"Minimum bet is **${SlotsService.MinBet:F2}**.");
            return;
        }
        if (bet > SlotsService.MaxBet)
        {
            await ReplyAsync($"Maximum bet is **${SlotsService.MaxBet:F2}**.");
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
        decimal vault = await economyService.GetVaultAmount();
        StringBuilder sb = new();

        sb.AppendLine($"**Community Vault: ${vault:N0}**");
        sb.AppendLine("Bets fund the vault. Wins are paid from the vault.");
        sb.AppendLine();

        sb.AppendLine("**Jackpots (Scaled by Bet/MaxBet):**");
        sb.AppendLine($"  💎 **Grand:** 500x + 25% of Vault (Max 50%) — *1 in 1,000,000*");
        sb.AppendLine($"  👑 **Major:** 100x + 15% of Vault (Max 25%) — *1 in 200,000*");
        sb.AppendLine($"  🏆 **Mini:**   50x + 5% of Vault (Max 10%) — *1 in 50,000*");
        sb.AppendLine("  *(Disabled if Vault < Max Liability)*");
        sb.AppendLine();

        sb.AppendLine("**Multipliers:**");
        sb.AppendLine("  🔔 **Mega:** 10x — *1 in 10,000*");
        sb.AppendLine("  🍓 **Big:** 5x — *1 in 2,000*");
        sb.AppendLine("  🍋 **Med:** 3x — *1 in 400*");
        sb.AppendLine("  🍊 **Small:** 2x — *1 in 83*");
        sb.AppendLine("  🍇 **Tiny:** 1.5x — *1 in 12*");
        sb.AppendLine("  🍒 **Push:** 1x — *1 in 7*");
        sb.AppendLine();

        sb.AppendLine("**Rules:**");
        sb.AppendLine($"  Min bet: **${SlotsService.MinBet:F2}** | Max bet: **${SlotsService.MaxBet:F2}**");
        sb.AppendLine("  5% tax is deducted from PROFIT only (goes to UBI).");
        sb.AppendLine("  Loss rate is approx 75%. Play responsibly!");

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle("🎰 Slots Paytable")
            .WithDescription(sb.ToString())
            .WithColor(Color.Blue)
            .WithCurrentTimestamp();

        await ReplyAsync(embed: embed.Build());
    }
}
