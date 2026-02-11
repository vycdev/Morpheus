using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Services;
using Morpheus.Utilities;
using System.Text;

namespace Morpheus.Modules;

[Name("Stocks")]
[Summary("Pseudo stock market — invest in users, guilds, and channels.")]
public class StocksModule(DB dbContext, StocksService stocksService, ChannelService channelService, UsersService usersService) : ModuleBase<SocketCommandContextExtended>
{
    #region Target Resolution

    /// <summary>
    /// Resolves a target string into a StockEntityType + entity DB ID.
    /// Supports: user mentions, #channel mentions, and "guild"/"server" keyword.
    /// </summary>
    private async Task<(bool success, StockEntityType type, int entityId, string displayName)?> ResolveTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return null;

        string trimmed = target.Trim().ToLower();

        // Guild / server
        if (trimmed is "guild" or "server")
        {
            Guild? guild = Context.DbGuild;
            if (guild == null) return null;
            return (true, StockEntityType.Guild, guild.Id, guild.Name);
        }

        // User mention: <@123> or <@!123>
        if (MentionUtils.TryParseUser(target, out ulong userId))
        {
            SocketUser? socketUser = Context.Client.GetUser(userId);
            if (socketUser == null) return null;

            User dbUser = await usersService.TryGetCreateUser(socketUser);
            return (true, StockEntityType.User, dbUser.Id, dbUser.Username);
        }

        // Channel mention: <#123>
        if (MentionUtils.TryParseChannel(target, out ulong channelId))
        {
            SocketChannel? socketChannel = Context.Client.GetChannel(channelId);
            string channelName = (socketChannel as SocketGuildChannel)?.Name ?? $"channel-{channelId}";

            Channel dbChannel = await channelService.TryGetCreateChannel(channelId, channelName);
            return (true, StockEntityType.Channel, dbChannel.Id, channelName);
        }

        return null;
    }

    #endregion

    #region Stock Commands

    [Name("Stock Buy")]
    [Summary("Buy shares of a user, guild, or channel stock. 5% deposit fee applies.")]
    [Command("stock buy")]
    [Alias("stock invest", "invest")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(5, 30)]
    public async Task StockBuyAsync(string target, decimal amount)
    {
        User? user = Context.DbUser;
        if (user == null) { await ReplyAsync("User not found."); return; }

        var resolved = await ResolveTarget(target);
        if (resolved == null) { await ReplyAsync("Invalid target. Mention a user, #channel, or use `guild`."); return; }
        var (_, entityType, entityId, displayName) = resolved.Value;

        Stock stock = await stocksService.GetOrCreateStock(entityType, entityId);
        var (success, message, _) = await stocksService.BuyStock(user.Id, stock.Id, amount);

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle(success ? $"📈 Bought {entityType} Stock" : "❌ Purchase Failed")
            .WithDescription(message)
            .WithColor(success ? Color.Green : Color.Red)
            .WithFooter($"Stock: {displayName} ({entityType})")
            .WithCurrentTimestamp();

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Stock Sell")]
    [Summary("Sell shares of a stock. 5% withdrawal fee applies.")]
    [Command("stock sell")]
    [Alias("stock withdraw", "withdraw")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(5, 30)]
    public async Task StockSellAsync(string target, string amountStr = "all")
    {
        User? user = Context.DbUser;
        if (user == null) { await ReplyAsync("User not found."); return; }

        var resolved = await ResolveTarget(target);
        if (resolved == null) { await ReplyAsync("Invalid target. Mention a user, #channel, or use `guild`."); return; }
        var (_, entityType, entityId, displayName) = resolved.Value;

        Stock? stock = await dbContext.Stocks
            .FirstOrDefaultAsync(s => s.EntityType == entityType && s.EntityId == entityId);

        if (stock == null) { await ReplyAsync("No stock found for that target."); return; }

        decimal? sharesToSell = null;
        if (amountStr.ToLower() != "all")
        {
            if (!decimal.TryParse(amountStr, out decimal parsed) || parsed <= 0)
            {
                await ReplyAsync("Invalid amount. Use a positive number or `all`.");
                return;
            }
            sharesToSell = parsed;
        }

        var (success, message, _) = await stocksService.SellStock(user.Id, stock.Id, sharesToSell);

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle(success ? $"📉 Sold {entityType} Stock" : "❌ Sale Failed")
            .WithDescription(message)
            .WithColor(success ? Color.Green : Color.Red)
            .WithFooter($"Stock: {displayName} ({entityType})")
            .WithCurrentTimestamp();

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Stock Portfolio")]
    [Summary("View your stock portfolio or another user's. Shows all holdings with current values.")]
    [Command("stock portfolio")]
    [Alias("stocks", "holdings", "portfolio")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task StockPortfolioAsync(IUser? mentionedUser = null)
    {
        User? targetDbUser;
        string displayName;

        if (mentionedUser != null)
        {
            targetDbUser = await dbContext.Users.FirstOrDefaultAsync(u => u.DiscordId == mentionedUser.Id);
            displayName = mentionedUser.Username;
        }
        else
        {
            targetDbUser = Context.DbUser;
            displayName = Context.User.Username;
        }

        if (targetDbUser == null) { await ReplyAsync("User not found."); return; }

        List<StockHolding> holdings = await stocksService.GetUserHoldings(targetDbUser.Id);

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle($"📊 {displayName}'s Portfolio")
            .WithColor(Colors.Blue)
            .WithCurrentTimestamp();

        if (holdings.Count == 0)
        {
            embed.WithDescription($"Balance: **${targetDbUser.Balance:F2}**\n\nNo stock holdings.");
            await ReplyAsync(embed: embed.Build());
            return;
        }

        decimal totalValue = 0m;
        decimal totalInvested = 0m;
        StringBuilder sb = new();
        sb.AppendLine($"💰 Balance: **${targetDbUser.Balance:F2}**\n");

        foreach (StockHolding holding in holdings)
        {
            if (holding.Stock == null) continue;

            string stockName = await stocksService.GetStockDisplayName(holding.Stock);
            decimal currentValue = holding.Shares * holding.Stock.Price;
            decimal pnl = currentValue - holding.TotalInvested;
            string pnlSign = pnl >= 0 ? "+" : "";
            string arrow = pnl >= 0 ? "🟢" : "🔴";
            string changeStr = holding.Stock.DailyChangePercent >= 0
                ? $"+{holding.Stock.DailyChangePercent:F2}%"
                : $"{holding.Stock.DailyChangePercent:F2}%";

            totalValue += currentValue;
            totalInvested += holding.TotalInvested;

            sb.AppendLine($"{arrow} **{stockName}** ({holding.Stock.EntityType})");
            sb.AppendLine($"  Shares: {holding.Shares:F4} @ ${holding.Stock.Price:F2} = **${currentValue:F2}**");
            sb.AppendLine($"  P&L: {pnlSign}${pnl:F2} | Today: {changeStr}");
            sb.AppendLine();
        }

        decimal totalPnl = totalValue - totalInvested;
        string totalPnlSign = totalPnl >= 0 ? "+" : "";
        sb.AppendLine($"**Total Holdings Value: ${totalValue:F2}**");
        sb.AppendLine($"**Total P&L: {totalPnlSign}${totalPnl:F2}**");
        sb.AppendLine($"**Net Worth: ${targetDbUser.Balance + totalValue:F2}**");

        embed.WithDescription(sb.ToString());
        await ReplyAsync(embed: embed.Build());
    }

    [Name("Stock Info")]
    [Summary("View stock information for a user, guild, or channel. Shows current price and daily change.")]
    [Command("stock info")]
    [Alias("stock price", "stockinfo", "stockprice")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task StockInfoAsync(string target)
    {
        var resolved = await ResolveTarget(target);
        if (resolved == null) { await ReplyAsync("Invalid target. Mention a user, #channel, or use `guild`."); return; }
        var (_, entityType, entityId, displayName) = resolved.Value;

        Stock? stock = await dbContext.Stocks
            .FirstOrDefaultAsync(s => s.EntityType == entityType && s.EntityId == entityId);

        if (stock == null)
        {
            await ReplyAsync("No stock exists for that target yet. Someone needs to invest first!");
            return;
        }

        string changeStr = stock.DailyChangePercent >= 0
            ? $"+{stock.DailyChangePercent:F2}%"
            : $"{stock.DailyChangePercent:F2}%";
        string arrow = stock.DailyChangePercent >= 0 ? "📈" : "📉";
        Color embedColor = stock.DailyChangePercent >= 0 ? Color.Green : Color.Red;

        int totalHolders = await dbContext.StockHoldings
            .CountAsync(sh => sh.StockId == stock.Id && sh.Shares > 0);

        decimal totalSharesHeld = await dbContext.StockHoldings
            .Where(sh => sh.StockId == stock.Id && sh.Shares > 0)
            .SumAsync(sh => sh.Shares);

        // Update time display
        int hours = stock.UpdateTimeMinutes / 60;
        int minutes = stock.UpdateTimeMinutes % 60;
        string updateTimeStr = $"{hours:D2}:{minutes:D2} UTC";

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle($"{arrow} {displayName} ({entityType} Stock)")
            .WithColor(embedColor)
            .AddField("Current Price", $"${stock.Price:F2}", true)
            .AddField("Previous Price", $"${stock.PreviousPrice:F2}", true)
            .AddField("Daily Change", changeStr, true)
            .AddField("Holders", totalHolders.ToString(), true)
            .AddField("Total Shares Held", $"{totalSharesHeld:F4}", true)
            .AddField("Daily Update Time", updateTimeStr, true)
            .WithFooter($"Last updated: {stock.LastUpdatedDate:yyyy-MM-dd HH:mm} UTC")
            .WithCurrentTimestamp();

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Stock Movers")]
    [Summary("Shows the top 5 biggest stock gainers and losers of the day.")]
    [Command("stock movers")]
    [Alias("stock top", "movers")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task StockMoversAsync()
    {
        var (gainers, losers) = await stocksService.GetTopMovers(5);

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle("📊 Today's Market Movers")
            .WithColor(Colors.Blue)
            .WithCurrentTimestamp();

        StringBuilder sb = new();

        if (gainers.Count > 0)
        {
            sb.AppendLine("**🟢 Top Gainers**");
            for (int i = 0; i < gainers.Count; i++)
            {
                string name = await stocksService.GetStockDisplayName(gainers[i]);
                sb.AppendLine($"{i + 1}. **{name}** ({gainers[i].EntityType}) — +{gainers[i].DailyChangePercent:F2}% (${gainers[i].Price:F2})");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("**🟢 Top Gainers** — None today\n");
        }

        if (losers.Count > 0)
        {
            sb.AppendLine("**🔴 Top Losers**");
            for (int i = 0; i < losers.Count; i++)
            {
                string name = await stocksService.GetStockDisplayName(losers[i]);
                sb.AppendLine($"{i + 1}. **{name}** ({losers[i].EntityType}) — {losers[i].DailyChangePercent:F2}% (${losers[i].Price:F2})");
            }
        }
        else
        {
            sb.AppendLine("**🔴 Top Losers** — None today");
        }

        embed.WithDescription(sb.ToString());
        await ReplyAsync(embed: embed.Build());
    }

    [Name("Stock Balance")]
    [Summary("Shows your cash balance, or another user's.")]
    [Command("stock balance")]
    [Alias("balance", "wallet", "bal")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task StockBalanceAsync(IUser? mentionedUser = null)
    {
        User? targetDbUser;
        string displayName;

        if (mentionedUser != null)
        {
            targetDbUser = await dbContext.Users.FirstOrDefaultAsync(u => u.DiscordId == mentionedUser.Id);
            displayName = mentionedUser.Username;
        }
        else
        {
            targetDbUser = Context.DbUser;
            displayName = Context.User.Username;
        }

        if (targetDbUser == null) { await ReplyAsync("User not found."); return; }

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle($"💰 {displayName}'s Balance")
            .WithDescription($"**${targetDbUser.Balance:F2}**")
            .WithColor(Colors.Blue)
            .WithCurrentTimestamp();

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Balance Leaderboard")]
    [Summary("Shows the top balances among users in this guild.")]
    [Command("balanceleaderboard")]
    [Alias("balancetop", "topbalance", "baltop")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task BalanceLeaderboardAsync(int page = 1)
    {
        Guild? guild = Context.DbGuild;
        var me = Context.DbUser;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        if (page < 1)
        {
            await ReplyAsync("Invalid page number. Please choose a page of 1 or higher.");
            return;
        }

        var userIdsQuery = dbContext.UserActivity.AsNoTracking()
            .Where(ua => ua.GuildId == guild.Id)
            .Select(ua => ua.UserId)
            .Distinct();

        var query = dbContext.Users.AsNoTracking()
            .Where(u => userIdsQuery.Contains(u.Id))
            .OrderByDescending(u => u.Balance);

        int totalUsers = await query.CountAsync();
        if (totalUsers == 0)
        {
            await ReplyAsync("No balance data found for this guild.");
            return;
        }

        int totalPages = (int)Math.Ceiling(totalUsers / 10.0);
        if (page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        var pageItems = await query
            .Skip((page - 1) * 10)
            .Take(10)
            .ToListAsync();

        IEnumerable<string> leaderboard = pageItems
            .Select((u, index) =>
            {
                string name = string.IsNullOrWhiteSpace(u.Username) ? u.DiscordId.ToString() : u.Username;
                return $"[{((page - 1) * 10) + index + 1}] | {name}: ${u.Balance:F2}";
            });

        StringBuilder sb = new();
        sb.AppendLine($"**Balance Leaderboard for {guild.Name}**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");

        string rankLine = "Your rank: N/A";
        if (me != null)
        {
            bool inGuild = await userIdsQuery.ContainsAsync(me.Id);
            if (inGuild)
            {
                int better = await dbContext.Users.AsNoTracking()
                    .Where(u => userIdsQuery.Contains(u.Id) && u.Balance > me.Balance)
                    .CountAsync();
                rankLine = $"Your rank: #{better + 1}";
            }
        }
        sb.AppendLine(rankLine);

        await ReplyAsync(sb.ToString());
    }

    [Name("Global Balance Leaderboard")]
    [Summary("Shows the top balances among users globally.")]
    [Command("globalbalanceleaderboard")]
    [Alias("globalbalancetop", "globaltopbalance", "gbaltop")]
    [RateLimit(3, 10)]
    public async Task GlobalBalanceLeaderboardAsync(int page = 1)
    {
        var me = Context.DbUser;

        if (page < 1)
        {
            await ReplyAsync("Invalid page number. Please choose a page of 1 or higher.");
            return;
        }

        var query = dbContext.Users.AsNoTracking()
            .OrderByDescending(u => u.Balance);

        int totalUsers = await query.CountAsync();
        if (totalUsers == 0)
        {
            await ReplyAsync("No global balance data found.");
            return;
        }

        int totalPages = (int)Math.Ceiling(totalUsers / 10.0);
        if (page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        var pageItems = await query
            .Skip((page - 1) * 10)
            .Take(10)
            .ToListAsync();

        IEnumerable<string> leaderboard = pageItems
            .Select((u, index) =>
            {
                string name = string.IsNullOrWhiteSpace(u.Username) ? u.DiscordId.ToString() : u.Username;
                return $"[{((page - 1) * 10) + index + 1}] | {name}: ${u.Balance:F2}";
            });

        StringBuilder sb = new();
        sb.AppendLine("**Global Balance Leaderboard**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");

        string rankLine = "Your rank: N/A";
        if (me != null)
        {
            int better = await dbContext.Users.AsNoTracking()
                .Where(u => u.Balance > me.Balance)
                .CountAsync();
            rankLine = $"Your rank: #{better + 1}";
        }
        sb.AppendLine(rankLine);

        await ReplyAsync(sb.ToString());
    }

    #endregion

    #region Transfer Command

    [Name("Transfer")]
    [Summary("Transfer money to another user. A 5% fee is charged on top (you pay amount + 5%).")]
    [Command("transfer")]
    [Alias("pay", "give")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 30)]
    public async Task TransferAsync(IUser targetUser, decimal amount)
    {
        User? sender = Context.DbUser;
        if (sender == null) { await ReplyAsync("User not found."); return; }

        User? receiver = await dbContext.Users.FirstOrDefaultAsync(u => u.DiscordId == targetUser.Id);
        if (receiver == null)
        {
            // Auto-create the receiver if they exist on Discord
            if (targetUser is SocketUser socketTarget)
                receiver = await usersService.TryGetCreateUser(socketTarget);
        }

        if (receiver == null) { await ReplyAsync("Recipient not found."); return; }

        var (success, message) = await stocksService.TransferMoney(sender.Id, receiver.Id, amount);

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle(success ? "💸 Transfer Complete" : "❌ Transfer Failed")
            .WithDescription(message)
            .WithColor(success ? Color.Green : Color.Red)
            .WithCurrentTimestamp();

        if (success)
            embed.WithFooter($"{Context.User.Username} → {targetUser.Username}");

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Wealth Leaderboard")]
    [Summary("Shows the top net worth (cash + holdings) among users in this guild.")]
    [Command("wealthleaderboard")]
    [Alias("wealthtop", "topwealth", "networthleaderboard")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task WealthLeaderboardAsync(int page = 1)
    {
        Guild? guild = Context.DbGuild;
        var me = Context.DbUser;
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        if (page < 1)
        {
            await ReplyAsync("Invalid page number. Please choose a page of 1 or higher.");
            return;
        }

        var userIdsQuery = dbContext.UserActivity.AsNoTracking()
            .Where(ua => ua.GuildId == guild.Id)
            .Select(ua => ua.UserId)
            .Distinct();

        var users = await dbContext.Users.AsNoTracking()
            .Where(u => userIdsQuery.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.DiscordId, u.Balance })
            .ToListAsync();

        if (users.Count == 0)
        {
            await ReplyAsync("No wealth data found for this guild.");
            return;
        }

        var holdingsValues = await dbContext.StockHoldings.AsNoTracking()
            .Where(sh => sh.Shares > 0)
            .Join(dbContext.Stocks.AsNoTracking(), sh => sh.StockId, s => s.Id,
                (sh, s) => new { sh.UserId, Value = sh.Shares * s.Price })
            .Where(x => userIdsQuery.Contains(x.UserId))
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Value = g.Sum(x => x.Value) })
            .ToListAsync();

        var holdingsMap = holdingsValues.ToDictionary(x => x.UserId, x => x.Value);

        var ranked = users
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.DiscordId,
                NetWorth = u.Balance + (holdingsMap.TryGetValue(u.Id, out var v) ? v : 0m)
            })
            .OrderByDescending(u => u.NetWorth)
            .ToList();

        int totalUsers = ranked.Count;
        int totalPages = (int)Math.Ceiling(totalUsers / 10.0);
        if (page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        var pageItems = ranked
            .Skip((page - 1) * 10)
            .Take(10)
            .ToList();

        IEnumerable<string> leaderboard = pageItems
            .Select((u, index) =>
            {
                string name = string.IsNullOrWhiteSpace(u.Username) ? u.DiscordId.ToString() : u.Username;
                return $"[{((page - 1) * 10) + index + 1}] | {name}: ${u.NetWorth:F2}";
            });

        StringBuilder sb = new();
        sb.AppendLine($"**Wealth Leaderboard for {guild.Name}**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");

        string rankLine = "Your rank: N/A";
        if (me != null)
        {
            bool inGuild = await userIdsQuery.ContainsAsync(me.Id);
            if (inGuild)
            {
                decimal myNetWorth = me.Balance + (holdingsMap.TryGetValue(me.Id, out var v) ? v : 0m);
                int better = ranked.Count(r => r.NetWorth > myNetWorth);
                rankLine = $"Your rank: #{better + 1}";
            }
        }
        sb.AppendLine(rankLine);

        await ReplyAsync(sb.ToString());
    }

    [Name("Global Wealth Leaderboard")]
    [Summary("Shows the top net worth (cash + holdings) among users globally.")]
    [Command("globalwealthleaderboard")]
    [Alias("globalwealthtop", "globaltopwealth", "globalnetworthleaderboard")]
    [RateLimit(3, 10)]
    public async Task GlobalWealthLeaderboardAsync(int page = 1)
    {
        var me = Context.DbUser;

        if (page < 1)
        {
            await ReplyAsync("Invalid page number. Please choose a page of 1 or higher.");
            return;
        }

        var users = await dbContext.Users.AsNoTracking()
            .Select(u => new { u.Id, u.Username, u.DiscordId, u.Balance })
            .ToListAsync();

        if (users.Count == 0)
        {
            await ReplyAsync("No global wealth data found.");
            return;
        }

        var holdingsValues = await dbContext.StockHoldings.AsNoTracking()
            .Where(sh => sh.Shares > 0)
            .Join(dbContext.Stocks.AsNoTracking(), sh => sh.StockId, s => s.Id,
                (sh, s) => new { sh.UserId, Value = sh.Shares * s.Price })
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Value = g.Sum(x => x.Value) })
            .ToListAsync();

        var holdingsMap = holdingsValues.ToDictionary(x => x.UserId, x => x.Value);

        var ranked = users
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.DiscordId,
                NetWorth = u.Balance + (holdingsMap.TryGetValue(u.Id, out var v) ? v : 0m)
            })
            .OrderByDescending(u => u.NetWorth)
            .ToList();

        int totalUsers = ranked.Count;
        int totalPages = (int)Math.Ceiling(totalUsers / 10.0);
        if (page > totalPages)
        {
            await ReplyAsync($"Invalid page number. Please choose a page between 1 and {totalPages}.");
            return;
        }

        var pageItems = ranked
            .Skip((page - 1) * 10)
            .Take(10)
            .ToList();

        IEnumerable<string> leaderboard = pageItems
            .Select((u, index) =>
            {
                string name = string.IsNullOrWhiteSpace(u.Username) ? u.DiscordId.ToString() : u.Username;
                return $"[{((page - 1) * 10) + index + 1}] | {name}: ${u.NetWorth:F2}";
            });

        StringBuilder sb = new();
        sb.AppendLine("**Global Wealth Leaderboard**");
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", leaderboard));
        sb.AppendLine($"\n(Page {page}/{totalPages})");
        sb.AppendLine("```");

        string rankLine = "Your rank: N/A";
        if (me != null)
        {
            decimal myNetWorth = me.Balance + (holdingsMap.TryGetValue(me.Id, out var v) ? v : 0m);
            int better = ranked.Count(r => r.NetWorth > myNetWorth);
            rankLine = $"Your rank: #{better + 1}";
        }
        sb.AppendLine(rankLine);

        await ReplyAsync(sb.ToString());
    }

    #endregion
}
