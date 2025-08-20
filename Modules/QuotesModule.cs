using Discord.Commands;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Utilities;
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

    // URL detection moved to Morpheus.Utilities.ContentUtils

    [Name("List Quotes")]
    [Summary("Lists quotes for the current guild (paginated).")]
    [Command("listquotes")]
    [Alias("quotes", "q")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
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

    [Name("List Global Quotes")]
    [Summary("Lists quotes across all guilds (paginated).")]
    [Command("listquotesglobal")]
    [Alias("quotesglobal", "qglobal")]
    [RateLimit(3, 10)]
    public async Task ListQuotesGlobal(int page = 1, string sort = "oldest", bool approvedOnly = false)
    {
        const int pageSize = 10;

        var total = await db.Quotes.AsNoTracking().Where(q => !q.Removed).CountAsync();
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        if (totalPages == 0) totalPages = 1;
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;

        // Exclude removed quotes from listings
        var quotesQuery = db.Quotes.AsNoTracking().Where(q => !q.Removed);
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
            .WithTitle($"Global Quotes — Page {page}/{totalPages} ({total} total)")
            .WithColor(Discord.Color.Blue)
            .WithCurrentTimestamp();

        foreach (var q in quotes)
        {
            scoreMap.TryGetValue(q.Id, out var s);
            userMap.TryGetValue(q.UserId, out var uname);
            var author = uname ?? "Unknown";
            var status = q.Approved ? "Approved" : "Pending";
            if (q.Removed) status += " (Removed)";

            var fieldName = $"#{q.Id} — Score: {s} — {status} — {author} — Guild: {q.GuildId}";
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
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task ShowQuote(int id)
    {
        var guildDb = Context.DbGuild!;
        var quote = await db.Quotes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id && !q.Removed);
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
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.AddReactions)]
    [RateLimit(3, 10)]
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

        // Prevent quotes that include attachments, embeds, or links
        if (Context.Message.Attachments != null && Context.Message.Attachments.Count > 0)
        {
            await ReplyAsync("Quotes cannot include attachments or images. Please provide only text.");
            return;
        }

        if (Context.Message.Embeds != null && Context.Message.Embeds.Any())
        {
            await ReplyAsync("Quotes cannot include embeds. Please provide only text.");
            return;
        }

        // reject obvious links using a more robust check (scheme urls, markdown links, bare domains, IPs)
        if (!string.IsNullOrWhiteSpace(text) && Utils.ContainsUrl(text))
        {
            await ReplyAsync("Quotes cannot contain links or URLs. Please remove any links and try again.");
            return;
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
    [RequireContext(ContextType.Guild)]
    [RequireBotPermission(GuildPermission.AddReactions)]
    [RateLimit(3, 10)]
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
    [RequireContext(ContextType.Guild)]
    [RateLimit(5, 10)]
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
            existing.UpdateDate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        var totalScore = await db.QuoteScores.Where(s => s.QuoteId == quote.Id).SumAsync(s => (int?)s.Score) ?? 0;
        await ReplyAsync($"Upvoted quote #{quote.Id} (+5). Current score: {totalScore}.");
    }

    [Name("Downvote Quote")]
    [Summary("Downvotes a quote by replying to the bot message (adds or updates your -5 score).")]
    [Command("downvote")]
    [Alias("dv")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(5, 10)]
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
            existing.UpdateDate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        var totalScore = await db.QuoteScores.Where(s => s.QuoteId == quote.Id).SumAsync(s => (int?)s.Score) ?? 0;
        await ReplyAsync($"Downvoted quote #{quote.Id} (-5). Current score: {totalScore}.");
    }

    [Name("Rate Quote")]
    [Summary("Rates a quote 1-10 by replying to the bot message; 1 => -5, 10 => +5.")]
    [Command("rate")]
    [RateLimit(5, 10)]
    [RequireContext(ContextType.Guild)]
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
            existing.UpdateDate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        var totalScore = await db.QuoteScores.Where(s => s.QuoteId == quote.Id).SumAsync(s => (int?)s.Score) ?? 0;
        await ReplyAsync($"Rated quote #{quote.Id} as {rating} ({(mapInt >= 0 ? "+" : "")}{mapInt}). Current score: {totalScore}.");
    }

    private static DateTime GetPeriodStart(string period)
    {
        var now = DateTime.UtcNow;
        return period.ToLowerInvariant() switch
        {
            "day" => now.Date,
            "week" => now.Date.AddDays(-(int)now.DayOfWeek),
            // construct month start as UTC-kind to avoid mixing unspecified kinds when sent to Postgres
            "month" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => now.Date
        };
    }

    private static (DateTime since, DateTime until) GetPreviousPeriodBounds(string period)
    {
        var now = DateTime.UtcNow;
        var currentStart = period.ToLowerInvariant() switch
        {
            "day" => now.Date,
            "week" => now.Date.AddDays(-(int)now.DayOfWeek),
            "month" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => now.Date
        };

        return period.ToLowerInvariant() switch
        {
            "day" => (currentStart.AddDays(-1), currentStart),
            "week" => (currentStart.AddDays(-7), currentStart),
            "month" => (currentStart.AddMonths(-1), currentStart),
            _ => (currentStart.AddDays(-1), currentStart)
        };
    }

    private async Task<(Quote? quote, int total)> GetTopQuoteSinceAsync(DateTime since, DateTime until, bool guildOnly)
    {
        // Use UpdateDate if present, otherwise InsertDate. Perform a DB-side join to avoid loading IDs in memory.
        var baseScores = db.QuoteScores.AsNoTracking().Where(s => (s.UpdateDate ?? s.InsertDate) >= since && (s.UpdateDate ?? s.InsertDate) < until);

        var joined = baseScores.Join(
            db.Quotes.AsNoTracking().Where(q => !q.Removed),
            s => s.QuoteId,
            q => q.Id,
            (s, q) => new { Score = s, Quote = q }
        );

        if (guildOnly)
        {
            var guildDb = Context.DbGuild!;
            joined = joined.Where(x => x.Quote.GuildId == guildDb.Id);
        }

        var grouped = await joined
            .GroupBy(x => x.Quote.Id)
            .Select(g => new { QuoteId = g.Key, Score = g.Sum(x => x.Score.Score) })
            .OrderByDescending(x => x.Score)
            .FirstOrDefaultAsync();

        if (grouped == null) return (null, 0);

        var quote = await db.Quotes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == grouped.QuoteId && !q.Removed);
        return (quote, grouped.Score);
    }

    [Command("quoteoftheday")]
    [Alias("qotd")]
    [Summary("Shows the quote with the highest total score in the last day. Use true to restrict to the current guild.")]
    public async Task QuoteOfTheDay(bool guildOnly = false)
    {
        var (since, until) = GetPreviousPeriodBounds("day");
        var (quote, total) = await GetTopQuoteSinceAsync(since, until, guildOnly);
        if (quote == null)
        {
            await ReplyAsync("No quote found for the period.");
            return;
        }
        await ReplyAsync($"Quote of the Day #{quote.Id} (Score: {total})\n{quote.Content}");
    }

    [Command("quoteoftheweek")]
    [Alias("qotw")]
    [Summary("Shows the quote with the highest total score in the last week. Use true to restrict to the current guild.")]
    public async Task QuoteOfTheWeek(bool guildOnly = false)
    {
        var (since, until) = GetPreviousPeriodBounds("week");
        var (quote, total) = await GetTopQuoteSinceAsync(since, until, guildOnly);
        if (quote == null)
        {
            await ReplyAsync("No quote found for the period.");
            return;
        }
        await ReplyAsync($"Quote of the Week #{quote.Id} (Score: {total})\n{quote.Content}");
    }

    [Command("quoteofthemonth")]
    [Alias("qotm")]
    [Summary("Shows the quote with the highest total score in the last month. Use true to restrict to the current guild.")]
    public async Task QuoteOfTheMonth(bool guildOnly = false)
    {
        var (since, until) = GetPreviousPeriodBounds("month");
        var (quote, total) = await GetTopQuoteSinceAsync(since, until, guildOnly);
        if (quote == null)
        {
            await ReplyAsync("No quote found for the period.");
            return;
        }
        await ReplyAsync($"Quote of the Month #{quote.Id} (Score: {total})\n{quote.Content}");
    }
}
