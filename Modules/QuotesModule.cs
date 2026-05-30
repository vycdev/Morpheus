using Discord.Commands;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Utilities;
using Microsoft.EntityFrameworkCore;
using Discord.WebSocket;
using Morpheus.Services;
using Morpheus.Handlers;
using Discord;

namespace Morpheus.Modules;

public class QuotesModule : ModuleBase<SocketCommandContextExtended>
{
    private readonly DB db;
    private readonly UsersService usersService;
    private readonly LogsService logsService;
    private readonly QuoteService quoteService;

    public QuotesModule(
        DB dbContext,
        UsersService usersService,
        LogsService logsService,
        InteractionsHandler interactionHandler,
        QuoteService quoteService)
    {
        db = dbContext;
        this.usersService = usersService;
        this.logsService = logsService;
        this.quoteService = quoteService;

        // Register interaction handler for quote approval buttons
        interactionHandler.RegisterInteraction("quote_approve", HandleQuoteApproveInteraction);
    }

    // Interaction handler for the approve button. Interaction custom id format: quote_approve:{approvalId}
    private async Task HandleQuoteApproveInteraction(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent comp)
        {
            try { await interaction.RespondAsync("Invalid interaction.", ephemeral: true); } catch (Exception ex) { Console.WriteLine($"[QuotesModule] RespondAsync failed: {ex}"); }
            return;
        }

        var custom = comp.Data.CustomId ?? string.Empty;

        // helper to respond safely (avoid double-response exceptions)
        async Task SafeRespond(string text)
        {
            try
            {
                await comp.RespondAsync(text, ephemeral: true);
            }
            catch (InvalidOperationException)
            {
                await comp.FollowupAsync(text, ephemeral: true);
            }
        }

        if (!custom.StartsWith("quote_approve:"))
        {
            return;
        }

        var parts = custom.Split(':', 2);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var approvalId))
        {
            await SafeRespond("Invalid approval identifier.");
            return;
        }

        var approval = await db.QuoteApprovalMessages.FirstOrDefaultAsync(a => a.Id == approvalId);
        if (approval == null)
        {
            await SafeRespond("Approval request not found.");
            return;
        }

        // Expiry read from env var QUOTE_APPROVAL_EXPIRY_DAYS (defaults to 5 days)
        int quoteApprovalExpiryDays = Env.Get<int>("QUOTE_APPROVAL_EXPIRY_DAYS", 5);

        if (approval.InsertDate.AddDays(quoteApprovalExpiryDays) < DateTime.UtcNow)
        {
            await SafeRespond($"This approval request has expired (valid for {quoteApprovalExpiryDays} days).");
            return;
        }

        if (approval.Approved)
        {
            await SafeRespond("This request has already been finalized.");
            return;
        }

        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == approval.QuoteId);
        if (quote == null)
        {
            await SafeRespond("Related quote not found.");
            return;
        }

        var guildDb = await db.Guilds.AsNoTracking().FirstOrDefaultAsync(g => g.Id == quote.GuildId);
        if (guildDb == null)
        {
            await SafeRespond("Guild configuration not found.");
            return;
        }

        var userDb = await usersService.TryGetCreateUser(comp.User);

        // prevent duplicate votes (DB unique index also protects against races)
        var already = await db.QuoteApprovals.AnyAsync(a => a.QuoteApprovalMessageId == approval.Id && a.UserId == (ulong)userDb.Id);
        if (already)
        {
            await SafeRespond("You have already approved this request.");
            return;
        }

        var vote = new QuoteApproval
        {
            QuoteApprovalMessageId = approval.Id,
            UserId = (ulong)userDb.Id,
            InsertDate = DateTime.UtcNow
        };

        try
        {
            await db.QuoteApprovals.AddAsync(vote);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Likely a duplicate due to race; treat as already approved by this user
            await SafeRespond("You have already approved this request.");
            return;
        }

        var count = await db.QuoteApprovals.CountAsync(a => a.QuoteApprovalMessageId == approval.Id);
        var required = approval.Type == QuoteApprovalType.AddRequest ? guildDb.QuoteAddRequiredApprovals : guildDb.QuoteRemoveRequiredApprovals;

        await SafeRespond($"Your approval was recorded. Current approvals: {count}/{required}");

        if (count < required)
            return;

        // finalize
        try
        {
            approval.Approved = true;
            if (approval.Type == QuoteApprovalType.AddRequest)
                quote.Approved = true;
            else
                quote.Removed = true;

            await db.SaveChangesAsync();

            // Edit the original approval message (if still present) to show final status and clear components
            if (approval.ApprovalMessageId != 0)
            {
                try
                {
                    // Prefer the channel from the interaction to avoid relying on Command Context (may be null)
                    IMessageChannel? channel = comp.Channel as IMessageChannel;
                    if (channel == null && Context != null)
                        channel = Context.Client.GetChannel(guildDb.QuotesApprovalChannelId) as IMessageChannel;

                    if (channel != null)
                    {
                        var fetched = await channel.GetMessageAsync(approval.ApprovalMessageId);
                        if (fetched is IUserMessage msg)
                        {
                            var statusText = approval.Type == QuoteApprovalType.AddRequest ? "✅ **ADD REQUEST APPROVED**" : "✅ **REMOVE REQUEST APPROVED**";
                            var safeContent = quote.Content ?? string.Empty;
                            var newContent = $"{statusText} — Quote #{quote.Id}\n\n```{safeContent}```";
                            var builder = new ComponentBuilder(); // empty clears components
                            await msg.ModifyAsync(props =>
                            {
                                props.Content = newContent;
                                props.Components = builder.Build();
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    // logsService may be null in rare cases; guard call
                    logsService?.Log($"Failed to update approval message {approval.ApprovalMessageId}: {ex}", LogSeverity.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            logsService.Log($"Error while finalizing approval {approval.Id}: {ex}", LogSeverity.Warning);
        }
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
        QuotePage quotePage = await quoteService.GetQuotePageAsync(page, sort, approvedOnly, guildDb.Id);
        if (!quotePage.HasItems)
        {
            await ReplyAsync("No quotes found on this page.");
            return;
        }

        page = quotePage.Page;
        var totalPages = quotePage.TotalPages;
        var total = quotePage.Total;
        var quotes = quotePage.Items;

        var embed = new Discord.EmbedBuilder()
            .WithTitle($"Quotes — Page {page}/{totalPages} ({total} total)")
            .WithColor(Discord.Color.Blue)
            .WithCurrentTimestamp();

        foreach (var q in quotes)
        {
            var status = q.Approved ? "Approved" : "Pending";
            if (q.Removed) status += " (Removed)";

            var fieldName = $"#{q.Id} — Score: {q.Score} — {status} — {q.Author}";
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
        QuotePage quotePage = await quoteService.GetQuotePageAsync(page, sort, approvedOnly, guildId: null);
        if (!quotePage.HasItems)
        {
            await ReplyAsync("No quotes found on this page.");
            return;
        }

        page = quotePage.Page;
        var totalPages = quotePage.TotalPages;
        var total = quotePage.Total;
        var quotes = quotePage.Items;
        var embed = new Discord.EmbedBuilder()
            .WithTitle($"Global Quotes — Page {page}/{totalPages} ({total} total)")
            .WithColor(Discord.Color.Blue)
            .WithCurrentTimestamp();

        foreach (var q in quotes)
        {
            var status = q.Approved ? "Approved" : "Pending";
            if (q.Removed) status += " (Removed)";

            var fieldName = $"#{q.Id} — Score: {q.Score} — {status} — {q.Author} — Guild: {q.GuildId}";
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
        QuoteDetails? quote = await quoteService.GetQuoteDetailsAsync(id);
        if (quote == null)
        {
            await ReplyAsync("Quote not found.");
            return;
        }

        var totalScore = quote.TotalScore;
        var author = quote.Author;

        var embed = new Discord.EmbedBuilder()
            .WithTitle($"Quote #{quote.Id}")
            .WithDescription(quote.Content)
            .AddField("Added by", author, true)
            .AddField("Score", totalScore.ToString(), true)
            .AddField("Status", QuoteService.FormatStatus(quote.Approved, quote.Removed), true)
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
        var approval = new QuoteApprovalMessage
        {
            QuoteId = quote.Id,
            Score = 0,
            Type = QuoteApprovalType.AddRequest,
            InsertDate = DateTime.UtcNow
        };

        await db.QuoteApprovalMessages.AddAsync(approval);
        await db.SaveChangesAsync();

        // Send a message in the approval channel with an up arrow reaction
        var channel = Context.Client.GetChannel(guildDb.QuotesApprovalChannelId) as IMessageChannel;
        if (channel != null)
        {
            try
            {
                var component = new ComponentBuilder()
                    .WithButton("Approve", customId: $"quote_approve:{approval.Id}", ButtonStyle.Primary)
                    .Build();

                var sent = await channel.SendMessageAsync($"📥 **ADD REQUEST — Quote #{quote.Id}**\nSubmitted by: {Context.User.Mention}\n\n```{text}```\nApprovals required: {guildDb.QuoteAddRequiredApprovals}", components: component);

                // store the approval message id so interaction handlers can map message -> approval
                approval.ApprovalMessageId = sent.Id;
                await db.SaveChangesAsync();
            }
            catch (Discord.Net.HttpException httpEx)
            {
                // Detect missing permissions reliably (Discord API error 50013 / "Missing Permissions")
                var msg = httpEx.Message ?? string.Empty;
                if (msg.Contains("Missing Permissions") || msg.Contains("50013"))
                {
                    logsService.Log($"Missing permission sending approval message for quote {quote.Id}: {httpEx}", LogSeverity.Warning);
                    await ReplyAsync("I don't have permission to post approval messages in the configured approval channel. Please grant me Send Messages (and Embed Links) permission for that channel.");
                    return;
                }

                // otherwise log and continue
                logsService.Log($"HTTP error sending approval message for quote {quote.Id}: {httpEx}", LogSeverity.Warning);
            }
            catch (Exception ex)
            {
                logsService.Log($"Failed to send quote approval message for quote {quote.Id}: {ex}", LogSeverity.Warning);
            }
        }
        else
        {
            await ReplyAsync("I cannot access the configured approval channel. Please ensure the channel exists and I have permission to view it.");
            return;
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
    public async Task RemoveQuote([Remainder] string input)
    {
        var guildDb = Context.DbGuild!;

        // Parse: removequote [force] <id> [reason]
        var trimmed = input.Trim();
        var forceFlag = false;
        if (trimmed.StartsWith("force ", StringComparison.OrdinalIgnoreCase))
        {
            forceFlag = true;
            trimmed = trimmed.Substring("force ".Length).TrimStart();
        }

        // Split remaining into id and optional reason
        var spaceIdx = trimmed.IndexOf(' ');
        var idStr = spaceIdx >= 0 ? trimmed.Substring(0, spaceIdx) : trimmed;
        var reason = spaceIdx >= 0 ? trimmed.Substring(spaceIdx + 1).Trim() : string.Empty;

        if (!int.TryParse(idStr, out var id))
        {
            await ReplyAsync("Invalid quote id. Usage: `removequote [force] <id> [reason]`");
            return;
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
            db.Quotes.Update(quote);
            await db.SaveChangesAsync();
            await ReplyAsync("Quote removed (admin force).");
            return;
        }

        var approval = new QuoteApprovalMessage
        {
            QuoteId = quote.Id,
            Score = 0,
            Type = QuoteApprovalType.RemoveRequest,
            InsertDate = DateTime.UtcNow
        };

        await db.QuoteApprovalMessages.AddAsync(approval);
        await db.SaveChangesAsync();

        var channel = Context.Client.GetChannel(guildDb.QuotesApprovalChannelId) as IMessageChannel;
        if (channel != null)
        {
            try
            {
                var component = new ComponentBuilder()
                    .WithButton("Approve", customId: $"quote_approve:{approval.Id}", ButtonStyle.Primary)
                    .Build();

                var sent = await channel.SendMessageAsync($"🗑️ **REMOVE REQUEST — Quote #{quote.Id}**\nRequested by: {Context.User.Mention}\n\n```{quote.Content}```\nApprovals required: {guildDb.QuoteRemoveRequiredApprovals}", components: component);

                approval.ApprovalMessageId = sent.Id;
                await db.SaveChangesAsync();
            }
            catch (Discord.Net.HttpException httpEx)
            {
                var msg = httpEx.Message ?? string.Empty;
                if (msg.Contains("Missing Permissions") || msg.Contains("50013"))
                {
                    logsService.Log($"Missing permission sending removal approval message for quote {quote.Id}: {httpEx}", LogSeverity.Warning);
                    await ReplyAsync("I don't have permission to post approval messages in the configured approval channel. Please grant me Send Messages (and Embed Links) permission for that channel.");
                    return;
                }

                logsService.Log($"HTTP error sending removal approval message for quote {quote.Id}: {httpEx}", LogSeverity.Warning);
            }
            catch (Exception ex)
            {
                logsService.Log($"Failed to send quote removal approval message for quote {quote.Id}: {ex}", LogSeverity.Warning);
            }
        }
        else
        {
            await ReplyAsync("I cannot access the configured approval channel. Please ensure the channel exists and I have permission to view it.");
            return;
        }

        await ReplyAsync("Quote removal submitted for approval.");
    }

    private async Task ScoreReferencedQuoteAsync(
        int score,
        string missingReferenceMessage,
        string wrongAuthorMessage,
        Func<QuoteScoreResult, string> successMessage)
    {
        if (Context.Message.ReferencedMessage == null)
        {
            await ReplyAsync(missingReferenceMessage);
            return;
        }

        if (await Context.Channel.GetMessageAsync(Context.Message.ReferencedMessage.Id) is not IUserMessage refMsg)
        {
            await ReplyAsync("Couldn't find the referenced message.");
            return;
        }

        if (refMsg.Author.Id != Context.Client.CurrentUser.Id)
        {
            await ReplyAsync(wrongAuthorMessage);
            return;
        }

        string quoteText = refMsg.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(quoteText))
        {
            await ReplyAsync("No quote text found in the referenced message.");
            return;
        }

        var guildDb = Context.DbGuild!;
        var userDb = await usersService.TryGetCreateUser(Context.User);
        QuoteScoreResult result = await quoteService.ScoreQuoteByContentAsync(
            quoteText,
            guildDb.Id,
            guildDb.UseGlobalQuotes,
            userDb.Id,
            score);

        if (!result.Found)
        {
            await ReplyAsync(result.ErrorMessage ?? "Couldn't find a matching quote.");
            return;
        }

        await ReplyAsync(successMessage(result));
    }

    [Name("Upvote Quote")]
    [Summary("Upvotes a quote by replying to the bot message (adds or updates your +5 score).")]
    [Command("upvote")]
    [Alias("uv")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(5, 10)]
    public async Task Upvote()
    {
        await ScoreReferencedQuoteAsync(
            score: 5,
            missingReferenceMessage: "Reply to a bot message that contains the quote text to upvote.",
            wrongAuthorMessage: "You must reply to a message from the bot to vote.",
            successMessage: result => $"Upvoted quote #{result.QuoteId} (+5). Current score: {result.TotalScore}.");
    }

    [Name("Downvote Quote")]
    [Summary("Downvotes a quote by replying to the bot message (adds or updates your -5 score).")]
    [Command("downvote")]
    [Alias("dv")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(5, 10)]
    public async Task Downvote()
    {
        await ScoreReferencedQuoteAsync(
            score: -5,
            missingReferenceMessage: "Reply to a bot message that contains the quote text to downvote.",
            wrongAuthorMessage: "You must reply to a message from the bot to vote.",
            successMessage: result => $"Downvoted quote #{result.QuoteId} (-5). Current score: {result.TotalScore}.");
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

        int score = QuoteService.MapRatingToScore(rating);
        await ScoreReferencedQuoteAsync(
            score,
            missingReferenceMessage: "Reply to the bot's approval message to rate.",
            wrongAuthorMessage: "You must reply to a message from the bot to rate.",
            successMessage: result => $"Rated quote #{result.QuoteId} as {rating} ({QuoteService.FormatSignedScore(result.AppliedScore)}). Current score: {result.TotalScore}.");
    }

    [Command("quoteoftheday")]
    [Alias("qotd")]
    [Summary("Shows the quote with the highest total score in the last day. Use true to restrict to the current guild.")]
    public async Task QuoteOfTheDay(bool guildOnly = false)
    {
        await ReplyTopQuoteForPeriodAsync("day", "Quote of the Day", guildOnly);
    }

    [Command("quoteoftheweek")]
    [Alias("qotw")]
    [Summary("Shows the quote with the highest total score in the last week. Use true to restrict to the current guild.")]
    public async Task QuoteOfTheWeek(bool guildOnly = false)
    {
        await ReplyTopQuoteForPeriodAsync("week", "Quote of the Week", guildOnly);
    }

    [Command("quoteofthemonth")]
    [Alias("qotm")]
    [Summary("Shows the quote with the highest total score in the last month. Use true to restrict to the current guild.")]
    public async Task QuoteOfTheMonth(bool guildOnly = false)
    {
        await ReplyTopQuoteForPeriodAsync("month", "Quote of the Month", guildOnly);
    }

    private async Task ReplyTopQuoteForPeriodAsync(string period, string title, bool guildOnly)
    {
        int? guildId = null;
        if (guildOnly)
        {
            var guildDb = Context.DbGuild;
            if (guildDb == null)
            {
                await ReplyAsync("Guild not found.");
                return;
            }

            guildId = guildDb.Id;
        }

        var (since, until) = QuoteService.GetPreviousPeriodBounds(period);
        QuotePeriodResult quote = await quoteService.GetTopQuoteSinceAsync(since, until, guildId);
        if (!quote.HasQuote)
        {
            await ReplyAsync("No quote found for the period.");
            return;
        }

        await ReplyAsync($"{title} #{quote.QuoteId} (Score: {quote.TotalScore})\n{quote.Content}");
    }
}
