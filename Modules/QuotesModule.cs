using Discord.Commands;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using System.Threading.Tasks;
using Discord.WebSocket;
using Morpheus.Services;
using Discord;

namespace Morpheus.Modules;

public class QuotesModule : ModuleBase<SocketCommandContextExtended>
{
    private readonly DB db;
    private readonly UsersService usersService;

    public QuotesModule(DB dbContext, UsersService usersService)
    {
        db = dbContext;
        this.usersService = usersService;
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
        if (guildDb.QuotesApprovalChannelId == 0)
        {
            // Check if caller is an administrator
            if (Context.User is SocketGuildUser gu && gu.GuildPermissions.Administrator)
            {
                quote.Approved = true;
                db.Quotes.Update(quote);
                await db.SaveChangesAsync();
                await ReplyAsync("Quote added and automatically approved (admin bypass).");
                return;
            }

            // No approval channel but non-admin => treat as submitted but no approvals will happen
            await ReplyAsync("Quote submitted for approval, but this server has no approval channel configured. An administrator can approve it manually.");
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

        await db.QuoteApproval.AddAsync(approval);
        await db.SaveChangesAsync();

        // Send a message in the approval channel with an up arrow reaction
        var channel = Context.Client.GetChannel(guildDb.QuotesApprovalChannelId) as IMessageChannel;
        if (channel != null)
        {
            try
            {
                var sent = await channel.SendMessageAsync($"Quote #{quote.Id} submitted for approval by {Context.User.Mention}:\n\"{text}\"\nApprovals: 0 / {guildDb.QuoteAddRequiredApprovals}");
                // add up arrow reaction
                await sent.AddReactionAsync(new Emoji("⬆️"));

                // store the approval message id so reaction handlers can map message -> approval
                approval.ApprovalMessageId = sent.Id;
                db.QuoteApproval.Update(approval);
                await db.SaveChangesAsync();
            }
            catch
            {
                // ignore failures to send or react
            }
        }

        await ReplyAsync("Quote submitted for approval.");
    }
}
