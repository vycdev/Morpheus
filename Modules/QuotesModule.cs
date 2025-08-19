using Discord.Commands;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Discord.WebSocket;
using Morpheus.Services;
using Discord;

namespace Morpheus.Modules;

public class QuotesModule : ModuleBase<SocketCommandContextExtended>
{
    private readonly DB db;
    private readonly UsersService usersService;
    private readonly LogsService logsService;

    public QuotesModule(DB dbContext, UsersService usersService, LogsService logsService)
    {
        db = dbContext;
        this.usersService = usersService;
        this.logsService = logsService;
    }

    [Name("List Quotes")]
    [Summary("Lists quotes for the current guild (paginated).")]
    [Command("listquotes")]
    [Alias("quotes", "q")]
    [RateLimit(3, 10)]
    [RequireDbGuild]
    public async Task ListQuotes(int page = 1, string sort = "oldest", bool approvedOnly = false)
    {
        var guildDb = Context.DbGuild!;
        const int pageSize = 10;

        var total = await db.Quotes.AsNoTracking().Where(q => q.GuildId == guildDb.Id && !q.Removed).CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        if (totalPages == 0) totalPages = 1;
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        // Exclude removed quotes from listings
        var quotesQuery = db.Quotes.AsNoTracking().Where(q => q.GuildId == guildDb.Id && !q.Removed);
        if (approvedOnly)
            quotesQuery = quotesQuery.Where(q => q.Approved && !q.Removed);

        quotesQuery = sort.ToLowerInvariant() switch
        {
            "top" or "top-rated" or "toprated" => quotesQuery
                .OrderByDescending(q => db.QuoteScores.Where(s => s.QuoteId == q.Id).Sum(s => s.Score)),
            "newest" => quotesQuery.OrderByDescending(q => q.InsertDate),
            _ => quotesQuery.OrderBy(q => q.InsertDate) // oldest
        };

        var quotes = await quotesQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (quotes.Count == 0)
        {
            await ReplyAsync("No quotes found on this page.");
            return;
        }

        var quoteIds = quotes.Select(q => q.Id).ToList();
        var scores = await db.QuoteScores.AsNoTracking()
            .Where(s => quoteIds.Contains(s.QuoteId))
            .GroupBy(s => s.QuoteId)
            .Select(g => new { Id = g.Key, Score = g.Sum(x => x.Score) })
            .ToListAsync();
        var scoreMap = scores.ToDictionary(s => s.Id, s => s.Score);

        var userIds = quotes.Select(q => q.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToListAsync();
        var userMap = users.ToDictionary(u => u.Id, u => u.Username);

        var embed = new Discord.EmbedBuilder()
            .WithTitle($"Quotes — Page {page}/{totalPages} ({total} total)")
            .WithColor(Discord.Color.Blue)
            .WithCurrentTimestamp();

        foreach (var q in quotes)
        {
            scoreMap.TryGetValue(q.Id, out var s);
            userMap.TryGetValue(q.UserId, out var uname);
            var author = uname ?? "Unknown";
            var status = q.Approved ? "Approved" : "Pending";
            if (q.Removed) status += " (Removed)";

            var fieldName = $"#{q.Id} — Score: {s} — {status} — {author}";
            var content = q.Content ?? string.Empty;
            const int maxContentLength = 300;
            if (content.Length > maxContentLength)
                content = content.Substring(0, maxContentLength - 1) + "…";
            var fieldValue = $"{content}\nInserted: {q.InsertDate.ToString("u")}";
            embed.AddField(fieldName, fieldValue, false);
        }

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Show Quote")]
    [Summary("Shows a single quote in full by id.")]
    [Command("showquote")]
    [Alias("quote", "showq")]
    [RateLimit(3, 10)]
    [RequireDbGuild]
    public async Task ShowQuote(int id)
    {
        var guildDb = Context.DbGuild!;
        var quote = await db.Quotes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id && q.GuildId == guildDb.Id && !q.Removed);
        if (quote == null)
        {
            await ReplyAsync("Quote not found.");
            return;
        }

        var totalScore = await db.QuoteScores.AsNoTracking().Where(s => s.QuoteId == quote.Id).SumAsync(s => (int?)s.Score) ?? 0;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == quote.UserId);
        var author = user?.Username ?? "Unknown";

        var embed = new Discord.EmbedBuilder()
            .WithTitle($"Quote #{quote.Id}")
            .WithDescription(quote.Content)
            .AddField("Added by", author, true)
            .AddField("Score", totalScore.ToString(), true)
            .AddField("Status", quote.Approved ? "Approved" : "Pending", true)
            .AddField("Removed", quote.Removed ? "Yes" : "No", true)
            .AddField("Inserted", quote.InsertDate.ToString("u"), true)
            .WithColor(Discord.Color.Gold)
            .WithCurrentTimestamp();

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Add Quote")]
    [Summary("Adds a quote to the guild (may require approval).")]
    [Command("addquote")]
    [Alias("quoteadd", "qadd")]
    [RateLimit(3, 10)]
    [RequireDbGuild]
    public async Task AddQuote([Remainder] string text)
    {
        // Must be in a guild (RequireDbGuild ensures Context.DbGuild exists)
        var guildDb = Context.DbGuild!;
        // support admin force flag by leading "force" token (e.g. `addquote force <text>`)
        var forceFlag = false;
        if (!string.IsNullOrWhiteSpace(text) && text.StartsWith("force ", StringComparison.OrdinalIgnoreCase))
        {
            forceFlag = true;
            text = text.Substring("force ".Length).TrimStart();
        }

        // Ensure user exists in DB
        var userDb = await usersService.TryGetCreateUser(Context.User);

        // Create quote record (not approved by default)
        var quote = new Quote
        {
            GuildId = guildDb.Id,
            UserId = userDb.Id,
            Content = text,
            Approved = false,
            Removed = false,
            InsertDate = DateTime.UtcNow
        };

        await db.Quotes.AddAsync(quote);
        await db.SaveChangesAsync(); // need Id

        // If no approval channel is set, admins can bypass and approve immediately
        var isAdmin = Context.User is SocketGuildUser guUser && guUser.GuildPermissions.Administrator;

        // only administrators may use the force flag
        if (forceFlag && !isAdmin)
        {
            await ReplyAsync("You cannot use the force flag unless you are an administrator.");
            return;
        }
        if (guildDb.QuotesApprovalChannelId == 0)
        {
            if (isAdmin)
            {
                quote.Approved = true;
                await db.SaveChangesAsync();
                await ReplyAsync("Quote added and automatically approved (admin bypass).");
                return;
            }

            // No approval channel but non-admin => treat as submitted but no approvals will happen
            await ReplyAsync("Quote submitted for approval, but this server has no approval channel configured. An administrator can approve it manually.");
            return;
        }

        // Approval channel exists: allow admin force to bypass approvals
        if (isAdmin && forceFlag)
        {
            quote.Approved = true;
            await db.SaveChangesAsync();
            await ReplyAsync("Quote added and automatically approved (admin force).");
            return;
        }

        // Approval channel exists: create a QuoteApprovals entry and post a message to the approval channel
        var approval = new QuoteApproval
        {
            QuoteId = quote.Id,
            Score = 0,
            Type = QuoteApprovalType.AddRequest,
            InsertDate = DateTime.UtcNow
        };

        await db.QuoteApprovals.AddAsync(approval);
        await db.SaveChangesAsync();

        // Send a message in the approval channel with an up arrow reaction
        var channel = Context.Client.GetChannel(guildDb.QuotesApprovalChannelId) as IMessageChannel;
        if (channel != null)
        {
            try
            {
                var sent = await channel.SendMessageAsync($"📥 **ADD REQUEST — Quote #{quote.Id}**\nSubmitted by: {Context.User.Mention}\n\n```{text}```\nApprovals: 0 / {guildDb.QuoteAddRequiredApprovals}");
                // add up arrow reaction
                await sent.AddReactionAsync(new Emoji("⬆️"));

                // store the approval message id so reaction handlers can map message -> approval
                approval.ApprovalMessageId = sent.Id;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logsService.Log($"Failed to send quote approval message for quote {quote.Id}: {ex}", LogSeverity.Warning);
            }
        }

        await ReplyAsync("Quote submitted for approval.");
    }

    [Name("Remove Quote")]
    [Summary("Requests removal of a quote (may require approval).")]
    [Command("removequote")]
    [Alias("quoteremove", "qremove", "remove")]
    [RateLimit(3, 10)]
    [RequireDbGuild]
    public async Task RemoveQuote(int id, [Remainder] string reason = "")
    {
        var guildDb = Context.DbGuild!;

        // support admin force flag by trailing " force" on the reason
        var forceFlag = false;
        if (!string.IsNullOrWhiteSpace(reason) && reason.EndsWith(" force", StringComparison.OrdinalIgnoreCase))
        {
            forceFlag = true;
            reason = reason.Substring(0, reason.Length - " force".Length).TrimEnd();
        }

        var quote = await db.Quotes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id);
        if (quote == null)
        {
            await ReplyAsync("Quote not found.");
            return;
        }

        if (quote.GuildId != guildDb.Id)
        {
            await ReplyAsync("You cannot remove a quote from another guild.");
            return;
        }

        if (quote.Removed)
        {
            await ReplyAsync("This quote is already removed.");
            return;
        }

        var isAdmin = Context.User is SocketGuildUser gu && gu.GuildPermissions.Administrator;

        // only administrators may use the force flag
        if (forceFlag && !isAdmin)
        {
            await ReplyAsync("You cannot use the force flag unless you are an administrator.");
            return;
        }

        // Disallow removal requests for quotes that aren't approved yet unless forced by admin
        if (!quote.Approved && !(isAdmin && forceFlag) && !forceFlag)
        {
            await ReplyAsync("This quote has not been approved yet and cannot be removed. An administrator may force removal with the force flag.");
            return;
        }

        // If no approval channel is set, admins can bypass and remove immediately
        if (guildDb.QuotesApprovalChannelId == 0)
        {
            if (isAdmin)
            {
                quote.Removed = true;
                db.Quotes.Update(quote);
                await db.SaveChangesAsync();
                await ReplyAsync("Quote removed (admin bypass).");
                return;
            }

            await ReplyAsync("Quote removal submitted for approval, but this server has no approval channel configured. An administrator can remove it manually.");
            return;
        }

        // If approval channel exists, allow admin force to bypass approvals
        if (isAdmin && forceFlag)
        {
            quote.Removed = true;
            await db.SaveChangesAsync();
            await ReplyAsync("Quote removed (admin force).");
            return;
        }

        var approval = new QuoteApproval
        {
            QuoteId = quote.Id,
            Score = 0,
            Type = QuoteApprovalType.RemoveRequest,
            InsertDate = DateTime.UtcNow
        };

        await db.QuoteApprovals.AddAsync(approval);
        await db.SaveChangesAsync();

        var channel = Context.Client.GetChannel(guildDb.QuotesApprovalChannelId) as IMessageChannel;
        if (channel != null)
        {
            try
            {
                var sent = await channel.SendMessageAsync($"🗑️ **REMOVE REQUEST — Quote #{quote.Id}**\nRequested by: {Context.User.Mention}\n\n```{quote.Content}```\nApprovals: 0 / {guildDb.QuoteRemoveRequiredApprovals}");
                await sent.AddReactionAsync(new Emoji("⬆️"));

                approval.ApprovalMessageId = sent.Id;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logsService.Log($"Failed to send quote removal approval message for quote {quote.Id}: {ex}", LogSeverity.Warning);
            }
        }

        await ReplyAsync("Quote removal submitted for approval.");
    }

    [Name("Upvote Quote")]
    [Summary("Upvotes a quote by replying to the bot message (adds or updates your +5 score).")]
    [Command("upvote")]
    [Alias("uv")]
    [RateLimit(5, 10)]
    [RequireDbGuild]
    public async Task Upvote()
    {
        if (Context.Message.ReferencedMessage == null)
        {
            await ReplyAsync("Reply to a bot message that contains the quote text to upvote.");
            return;
        }

        if (await Context.Channel.GetMessageAsync(Context.Message.ReferencedMessage.Id) is not IUserMessage refMsg)
        {
            await ReplyAsync("Couldn't find the referenced message.");
            return;
        }

        if (refMsg.Author.Id != Context.Client.CurrentUser.Id)
        {
            await ReplyAsync("You must reply to a message from the bot to vote.");
            return;
        }

        var qtext = refMsg.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(qtext))
        {
            await ReplyAsync("No quote text found in the referenced message.");
            return;
        }

        var guildDb = Context.DbGuild!;
        Quote? quote = null;
        if (guildDb.UseGlobalQuotes)
        {
            quote = await db.Quotes.FirstOrDefaultAsync(q => q.Approved && !q.Removed && (q.Content ?? string.Empty) == qtext);
        }
        else
        {
            quote = await db.Quotes.FirstOrDefaultAsync(q => q.Approved && !q.Removed && q.GuildId == guildDb.Id && (q.Content ?? string.Empty) == qtext);
        }

        if (quote == null)
        {
            await ReplyAsync("Couldn't find a matching quote.");
            return;
        }

        var userDb = await usersService.TryGetCreateUser(Context.User);
        var existing = await db.QuoteScores.FirstOrDefaultAsync(s => s.QuoteId == quote.Id && s.UserId == userDb.Id);
        if (existing == null)
        {
            var score = new QuoteScore { QuoteId = quote.Id, UserId = userDb.Id, Score = 5, InsertDate = DateTime.UtcNow };
            await db.QuoteScores.AddAsync(score);
        }
        else
        {
            existing.Score = 5;
        }

        await db.SaveChangesAsync();
        var totalScore = await db.QuoteScores.Where(s => s.QuoteId == quote.Id).SumAsync(s => (int?)s.Score) ?? 0;
        await ReplyAsync($"Upvoted quote #{quote.Id} (+5). Current score: {totalScore}.");
    }

    [Name("Downvote Quote")]
    [Summary("Downvotes a quote by replying to the bot message (adds or updates your -5 score).")]
    [Command("downvote")]
    [Alias("dv")]
    [RateLimit(5, 10)]
    [RequireDbGuild]
    public async Task Downvote()
    {
        if (Context.Message.ReferencedMessage == null)
        {
            await ReplyAsync("Reply to a bot message that contains the quote text to downvote.");
            return;
        }

        if (await Context.Channel.GetMessageAsync(Context.Message.ReferencedMessage.Id) is not IUserMessage refMsg)
        {
            await ReplyAsync("Couldn't find the referenced message.");
            return;
        }

        if (refMsg.Author.Id != Context.Client.CurrentUser.Id)
        {
            await ReplyAsync("You must reply to a message from the bot to vote.");
            return;
        }

        var qtext = refMsg.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(qtext))
        {
            await ReplyAsync("No quote text found in the referenced message.");
            return;
        }

        var guildDb = Context.DbGuild!;
        Quote? quote = null;
        if (guildDb.UseGlobalQuotes)
        {
            quote = await db.Quotes.FirstOrDefaultAsync(q => q.Approved && !q.Removed && (q.Content ?? string.Empty) == qtext);
        }
        else
        {
            quote = await db.Quotes.FirstOrDefaultAsync(q => q.Approved && !q.Removed && q.GuildId == guildDb.Id && (q.Content ?? string.Empty) == qtext);
        }

        if (quote == null)
        {
            await ReplyAsync("Couldn't find a matching quote.");
            return;
        }

        var userDb = await usersService.TryGetCreateUser(Context.User);
        var existing = await db.QuoteScores.FirstOrDefaultAsync(s => s.QuoteId == quote.Id && s.UserId == userDb.Id);
        if (existing == null)
        {
            var score = new QuoteScore { QuoteId = quote.Id, UserId = userDb.Id, Score = -5, InsertDate = DateTime.UtcNow };
            await db.QuoteScores.AddAsync(score);
        }
        else
        {
            existing.Score = -5;
        }

        await db.SaveChangesAsync();
        var totalScore = await db.QuoteScores.Where(s => s.QuoteId == quote.Id).SumAsync(s => (int?)s.Score) ?? 0;
        await ReplyAsync($"Downvoted quote #{quote.Id} (-5). Current score: {totalScore}.");
    }

    [Name("Rate Quote")]
    [Summary("Rates a quote 1-10 by replying to the bot message; 1 => -5, 10 => +5.")]
    [Command("rate")]
    [RateLimit(5, 10)]
    [RequireDbGuild]
    public async Task Rate(int rating)
    {
        if (rating < 1 || rating > 10)
        {
            await ReplyAsync("Rating must be between 1 and 10.");
            return;
        }

        if (Context.Message.ReferencedMessage == null)
        {
            await ReplyAsync("Reply to the bot's approval message to rate.");
            return;
        }

        if (await Context.Channel.GetMessageAsync(Context.Message.ReferencedMessage.Id) is not IUserMessage refMsg)
        {
            await ReplyAsync("Couldn't find the referenced message.");
            return;
        }

        if (refMsg.Author.Id != Context.Client.CurrentUser.Id)
        {
            await ReplyAsync("You must reply to a message from the bot to rate.");
            return;
        }

        var qtext = refMsg.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(qtext))
        {
            await ReplyAsync("No quote text found in the referenced message.");
            return;
        }

        var guildDb = Context.DbGuild!;
        Quote? quote = null;
        if (guildDb.UseGlobalQuotes)
        {
            quote = await db.Quotes.FirstOrDefaultAsync(q => q.Approved && !q.Removed && (q.Content ?? string.Empty) == qtext);
        }
        else
        {
            quote = await db.Quotes.FirstOrDefaultAsync(q => q.Approved && !q.Removed && q.GuildId == guildDb.Id && (q.Content ?? string.Empty) == qtext);
        }

        if (quote == null)
        {
            await ReplyAsync("Couldn't find a matching quote.");
            return;
        }

        // Map 1..10 -> -5..+5 linearly
        double mapped = -5 + (rating - 1) * (10.0 / 9.0);
        var mapInt = (int)Math.Round(mapped);
        if (mapInt < -5) mapInt = -5;
        if (mapInt > 5) mapInt = 5;

        var userDb = await usersService.TryGetCreateUser(Context.User);
        var existing = await db.QuoteScores.FirstOrDefaultAsync(s => s.QuoteId == quote.Id && s.UserId == userDb.Id);
        if (existing == null)
        {
            var score = new QuoteScore { QuoteId = quote.Id, UserId = userDb.Id, Score = mapInt, InsertDate = DateTime.UtcNow };
            await db.QuoteScores.AddAsync(score);
        }
        else
        {
            existing.Score = mapInt;
        }

        await db.SaveChangesAsync();
        var totalScore = await db.QuoteScores.Where(s => s.QuoteId == quote.Id).SumAsync(s => (int?)s.Score) ?? 0;
        await ReplyAsync($"Rated quote #{quote.Id} as {rating} ({(mapInt >= 0 ? "+" : "")}{mapInt}). Current score: {totalScore}.");
    }
}
