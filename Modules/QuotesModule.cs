using Discord.Commands;
using Morpheus.Attributes;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Utilities;
using Discord.WebSocket;
using Morpheus.Services;
using Morpheus.Handlers;
using Discord;

namespace Morpheus.Modules;

public class QuotesModule : ModuleBase<SocketCommandContextExtended>
{
    private readonly UsersService usersService;
    private readonly LogsService logsService;
    private readonly QuoteService quoteService;

    public QuotesModule(
        UsersService usersService,
        LogsService logsService,
        InteractionsHandler interactionHandler,
        QuoteService quoteService)
    {
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

        // Expiry read from env var QUOTE_APPROVAL_EXPIRY_DAYS (defaults to 5 days)
        int quoteApprovalExpiryDays = Env.Get<int>("QUOTE_APPROVAL_EXPIRY_DAYS", 5);
        var userDb = await usersService.TryGetCreateUser(comp.User);
        QuoteApprovalResult result = await quoteService.ApproveQuoteRequestAsync(
            approvalId,
            userDb.Id,
            quoteApprovalExpiryDays);

        switch (result.Status)
        {
            case QuoteApprovalResultStatus.NotFound:
                await SafeRespond("Approval request not found.");
                return;
            case QuoteApprovalResultStatus.Expired:
                await SafeRespond($"This approval request has expired (valid for {quoteApprovalExpiryDays} days).");
                return;
            case QuoteApprovalResultStatus.AlreadyFinalized:
                await SafeRespond("This request has already been finalized.");
                return;
            case QuoteApprovalResultStatus.QuoteNotFound:
                await SafeRespond("Related quote not found.");
                return;
            case QuoteApprovalResultStatus.GuildNotFound:
                await SafeRespond("Guild configuration not found.");
                return;
            case QuoteApprovalResultStatus.Duplicate:
                await SafeRespond("You have already approved this request.");
                return;
        }

        await SafeRespond($"Your approval was recorded. Current approvals: {result.CurrentApprovals}/{result.RequiredApprovals}");

        if (!result.IsFinalized)
            return;

        await UpdateApprovalMessageAsync(comp, result);
    }

    private async Task UpdateApprovalMessageAsync(SocketMessageComponent component, QuoteApprovalResult result)
    {
        if (result.ApprovalMessageId == 0)
            return;

        try
        {
            IMessageChannel? channel = component.Channel as IMessageChannel;
            if (channel == null && Context != null)
                channel = Context.Client.GetChannel(result.QuotesApprovalChannelId) as IMessageChannel;

            if (channel == null)
                return;

            var fetched = await channel.GetMessageAsync(result.ApprovalMessageId);
            if (fetched is not IUserMessage message)
                return;

            string statusText = result.Type == QuoteApprovalType.AddRequest
                ? "**ADD REQUEST APPROVED**"
                : "**REMOVE REQUEST APPROVED**";
            string newContent = $"{statusText} - Quote #{result.QuoteId}\n\n```{result.QuoteContent}```";
            ComponentBuilder builder = new();
            await message.ModifyAsync(props =>
            {
                props.Content = newContent;
                props.Components = builder.Build();
            });
        }
        catch (Exception ex)
        {
            logsService.Log($"Failed to update approval message {result.ApprovalMessageId}: {ex}", LogSeverity.Warning);
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
        var guildDb = Context.DbGuild!;
        var forceFlag = false;
        if (!string.IsNullOrWhiteSpace(text) && text.StartsWith("force ", StringComparison.OrdinalIgnoreCase))
        {
            forceFlag = true;
            text = text.Substring("force ".Length).TrimStart();
        }

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

        if (!string.IsNullOrWhiteSpace(text) && Utils.ContainsUrl(text))
        {
            await ReplyAsync("Quotes cannot contain links or URLs. Please remove any links and try again.");
            return;
        }

        var isAdmin = Context.User is SocketGuildUser guUser && guUser.GuildPermissions.Administrator;
        if (forceFlag && !isAdmin)
        {
            await ReplyAsync("You cannot use the force flag unless you are an administrator.");
            return;
        }

        var userDb = await usersService.TryGetCreateUser(Context.User);
        QuoteAddRequestResult result = await quoteService.CreateAddRequestAsync(
            guildDb.Id,
            userDb.Id,
            text,
            isAdmin,
            forceFlag,
            guildDb.QuotesApprovalChannelId,
            guildDb.QuoteAddRequiredApprovals);

        if (result.Status == QuoteAddRequestStatus.Approved)
        {
            await ReplyAsync(forceFlag
                ? "Quote added and automatically approved (admin force)."
                : "Quote added and automatically approved (admin bypass).");
            return;
        }

        if (result.Status == QuoteAddRequestStatus.PendingWithoutApprovalChannel)
        {
            await ReplyAsync("Quote submitted for approval, but this server has no approval channel configured. An administrator can approve it manually.");
            return;
        }

        if (!await TryPostQuoteApprovalRequestAsync(new QuoteApprovalPostRequest(
            result.ApprovalId,
            result.ApprovalChannelId,
            result.QuoteId,
            result.QuoteContent,
            result.RequiredApprovals,
            "ADD REQUEST",
            "Submitted by",
            "approval message",
            "quote approval message")))
            return;

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
        var trimmed = input.Trim();
        var forceFlag = false;
        if (trimmed.StartsWith("force ", StringComparison.OrdinalIgnoreCase))
        {
            forceFlag = true;
            trimmed = trimmed.Substring("force ".Length).TrimStart();
        }

        var spaceIdx = trimmed.IndexOf(' ');
        var idStr = spaceIdx >= 0 ? trimmed.Substring(0, spaceIdx) : trimmed;

        if (!int.TryParse(idStr, out var id))
        {
            await ReplyAsync("Invalid quote id. Usage: `removequote [force] <id> [reason]`");
            return;
        }

        var isAdmin = Context.User is SocketGuildUser gu && gu.GuildPermissions.Administrator;
        if (forceFlag && !isAdmin)
        {
            await ReplyAsync("You cannot use the force flag unless you are an administrator.");
            return;
        }

        QuoteRemoveRequestResult result = await quoteService.CreateRemoveRequestAsync(
            guildDb.Id,
            id,
            isAdmin,
            forceFlag,
            guildDb.QuotesApprovalChannelId,
            guildDb.QuoteRemoveRequiredApprovals);

        switch (result.Status)
        {
            case QuoteRemoveRequestStatus.NotFound:
                await ReplyAsync("Quote not found.");
                return;
            case QuoteRemoveRequestStatus.WrongGuild:
                await ReplyAsync("You cannot remove a quote from another guild.");
                return;
            case QuoteRemoveRequestStatus.AlreadyRemoved:
                await ReplyAsync("This quote is already removed.");
                return;
            case QuoteRemoveRequestStatus.NotApproved:
                await ReplyAsync("This quote has not been approved yet and cannot be removed. An administrator may force removal with the force flag.");
                return;
            case QuoteRemoveRequestStatus.Removed:
                await ReplyAsync(forceFlag
                    ? "Quote removed (admin force)."
                    : "Quote removed (admin bypass).");
                return;
            case QuoteRemoveRequestStatus.PendingWithoutApprovalChannel:
                await ReplyAsync("Quote removal submitted for approval, but this server has no approval channel configured. An administrator can remove it manually.");
                return;
        }

        if (!await TryPostQuoteApprovalRequestAsync(new QuoteApprovalPostRequest(
            result.ApprovalId,
            result.ApprovalChannelId,
            result.QuoteId,
            result.QuoteContent,
            result.RequiredApprovals,
            "REMOVE REQUEST",
            "Requested by",
            "removal approval message",
            "quote removal approval message")))
            return;

        await ReplyAsync("Quote removal submitted for approval.");
    }

    private async Task<bool> TryPostQuoteApprovalRequestAsync(QuoteApprovalPostRequest request)
    {
        var channel = Context.Client.GetChannel(request.ApprovalChannelId) as IMessageChannel;
        if (channel == null)
        {
            await quoteService.AbandonApprovalRequestAsync(request.ApprovalId);
            await ReplyAsync("I cannot access the configured approval channel. Please ensure the channel exists and I have permission to view it.");
            return false;
        }

        IUserMessage sent;
        try
        {
            var component = new ComponentBuilder()
                .WithButton("Approve", customId: $"quote_approve:{request.ApprovalId}", ButtonStyle.Primary)
                .Build();

            string message = $"{request.Heading} - Quote #{request.QuoteId}\n{request.RequesterLabel}: {Context.User.Mention}\n\n```{request.QuoteContent}```\nApprovals required: {request.RequiredApprovals}";
            sent = await channel.SendMessageAsync(message, components: component);
        }
        catch (Discord.Net.HttpException httpEx)
        {
            await quoteService.AbandonApprovalRequestAsync(request.ApprovalId);
            var msg = httpEx.Message ?? string.Empty;
            if (msg.Contains("Missing Permissions") || msg.Contains("50013"))
            {
                logsService.Log($"Missing permission sending {request.LogAction} for quote {request.QuoteId}: {httpEx}", LogSeverity.Warning);
                await ReplyAsync("I don't have permission to post approval messages in the configured approval channel. Please grant me Send Messages (and Embed Links) permission for that channel.");
                return false;
            }

            logsService.Log($"HTTP error sending {request.LogAction} for quote {request.QuoteId}: {httpEx}", LogSeverity.Warning);
            await ReplyAsync($"Failed to post the {request.FailureAction}, so the pending request was cleaned up.");
            return false;
        }
        catch (Exception ex)
        {
            await quoteService.AbandonApprovalRequestAsync(request.ApprovalId);
            logsService.Log($"Failed to send {request.FailureAction} for quote {request.QuoteId}: {ex}", LogSeverity.Warning);
            await ReplyAsync($"Failed to post the {request.FailureAction}, so the pending request was cleaned up.");
            return false;
        }

        try
        {
            if (!await quoteService.RecordApprovalMessageIdAsync(request.ApprovalId, sent.Id))
                logsService.Log($"Approval message {sent.Id} was posted for quote {request.QuoteId}, but approval request {request.ApprovalId} was not found when storing the message id.", LogSeverity.Warning);
        }
        catch (Exception ex)
        {
            logsService.Log($"Approval message {sent.Id} was posted for quote {request.QuoteId}, but storing the message id failed: {ex}", LogSeverity.Warning);
        }

        return true;
    }

    private sealed record QuoteApprovalPostRequest(
        int ApprovalId,
        ulong ApprovalChannelId,
        int QuoteId,
        string QuoteContent,
        int RequiredApprovals,
        string Heading,
        string RequesterLabel,
        string LogAction,
        string FailureAction);

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
