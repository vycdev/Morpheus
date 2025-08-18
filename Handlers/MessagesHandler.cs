using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Services;
using Morpheus.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Morpheus.Utilities.Lists;
using System.Reflection;

namespace Morpheus.Handlers;

public class MessagesHandler
{
    private readonly DiscordSocketClient client;
    private readonly CommandService commands;
    private readonly IServiceProvider serviceProvider;
    private readonly GuildService guildService;
    private readonly UsersService usersService;
    private readonly bool started = false;

    public MessagesHandler(DiscordSocketClient client, CommandService commands, IServiceProvider serviceProvider, GuildService guildService, UsersService usersService)
    {
        if (started)
            throw new InvalidOperationException("At most one instance of this service can be started");

        started = true;

        this.client = client;
        this.commands = commands;
        this.serviceProvider = serviceProvider;
        this.guildService = guildService;
        this.usersService = usersService;

        client.MessageReceived += HandleMessageAsync;
        client.ReactionAdded += HandleReactionAdded;
        client.ReactionRemoved += HandleReactionRemoved;
    }

    public async Task InstallCommands()
    {
        await commands.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);
    }

    private async Task HandleMessageAsync(SocketMessage messageParam)
    {
        // Don't process the command if it was a system message
        if (messageParam is not SocketUserMessage message)
            return;

        // Create a number to track where the prefix ends and the command begins
        int argPos = 0;

        User user = await usersService.TryGetCreateUser(message.Author);
        await usersService.TryUpdateUsername(message.Author, user);

        // If the message is in a guild, try to get the guild from the database
        // If the guild doesn't exist, create it and then get it
        Guild? guild = null;
        if (message.Channel is SocketGuildChannel guildChannel)
            guild = await guildService.TryGetCreateGuild(guildChannel.Guild);

        // Determine if the message is a command based on the prefix and make sure no bots trigger commands
        if (!(message.HasStringPrefix(guild?.Prefix ?? Env.Variables["BOT_DEFAULT_COMMAND_PREFIX"], ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos)) || message.Author.IsBot)
            return;

        // Create a WebSocket-based command context based on the message
        SocketCommandContextExtended context = new(client, message, guild, user);

        // Execute the command with the command context we just
        // created, along with the service provider for precondition checks.
        IResult result = await commands.ExecuteAsync(context, argPos, serviceProvider);

        if (result.IsSuccess)
            return;

        _ = result.Error switch
        {
            CommandError.UnknownCommand => await context.Channel.SendMessageAsync("Unknown command."),
            CommandError.BadArgCount => await context.Channel.SendMessageAsync("Invalid number of arguments."),
            CommandError.ParseFailed => await context.Channel.SendMessageAsync("Failed to parse arguments."),
            CommandError.ObjectNotFound => await context.Channel.SendMessageAsync("Object not found."),
            CommandError.MultipleMatches => await context.Channel.SendMessageAsync("Multiple matches found."),
            CommandError.UnmetPrecondition => await context.Channel.SendMessageAsync(result.ErrorReason),
            CommandError.Exception => await context.Channel.SendMessageAsync("An exception occurred."),
            CommandError.Unsuccessful => await context.Channel.SendMessageAsync("Unsuccessful."),
            _ => await context.Channel.SendMessageAsync("An unknown error occurred.")
        };
    }

    private async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channelCache, SocketReaction reaction)
    {
        await ProcessReactionChange(cache, channelCache, reaction, true);
    }

    private async Task HandleReactionRemoved(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channelCache, SocketReaction reaction)
    {
        await ProcessReactionChange(cache, channelCache, reaction, false);
    }

    private async Task ProcessReactionChange(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channelCache, SocketReaction reaction, bool added)
    {
        // Only handle the up arrow emoji used for approvals
        if (reaction.Emote is not Emoji emoji)
            return;

        if (emoji.Name != "⬆️")
            return;

        // Try to get the message id
        var messageId = reaction.MessageId;

        // Look up approval by ApprovalMessageId
        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = (Database.DB)scope.ServiceProvider.GetRequiredService(typeof(Database.DB));

            var approval = await db.QuoteApproval.FirstOrDefaultAsync(a => a.ApprovalMessageId == (ulong)messageId);
            if (approval == null)
                return; // not an approval message

            // ignore if this approval is already finalized
            if (approval.Approved)
                return;

            // Recalculate score from message reactions to avoid drift. We only handle the up-arrow
            var cachedMessage = await cache.GetOrDownloadAsync();
            var newScore = 0;
            if (cachedMessage != null)
            {
                if (cachedMessage.Reactions.TryGetValue(new Emoji("⬆️"), out var reactionMetadata))
                {
                    // Reaction count includes the bot's reaction; subtract 1 if the bot reacted.
                    newScore = (int)reactionMetadata.ReactionCount;
                    try
                    {
                        var selfUser = client.CurrentUser;
                        if (reactionMetadata.IsMe)
                            newScore -= 1;
                    }
                    catch { }
                    if (newScore < 0) newScore = 0;
                }
            }

            approval.Score = newScore;
            db.QuoteApproval.Update(approval);
            await db.SaveChangesAsync();

            // get guild settings to see required approvals
            var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == approval.QuoteId);
            if (quote == null)
                return;

            var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Id == quote.GuildId);
            if (guild == null)
                return;

            // Update the approval message content to show "x out of y"
            var channel = await channelCache.GetOrDownloadAsync();
            if (channel is IMessageChannel msgChannel)
            {
                var approvalMessage = await cache.GetOrDownloadAsync();
                if (approvalMessage != null)
                {
                    try
                    {
                        var required = approval.Type == Database.Models.QuoteApprovalType.AddRequest ? guild.QuoteAddRequiredApprovals : guild.QuoteRemoveRequiredApprovals;
                        var newContent = $"Quote #{quote.Id} submitted for approval:\n\"{quote.Content}\"\nApprovals: {approval.Score} / {required}";
                        await approvalMessage.ModifyAsync(m => m.Content = newContent);
                    }
                    catch
                    {
                        // ignore modify failures
                    }
                }
            }

            // If approvals reached threshold, mark quote approved
            var requiredApprovals = approval.Type == Database.Models.QuoteApprovalType.AddRequest ? guild.QuoteAddRequiredApprovals : guild.QuoteRemoveRequiredApprovals;
            if (!approval.Approved && approval.Score >= requiredApprovals)
            {
                // mark this approval entry as approved so further reactions are ignored
                approval.Approved = true;
                db.QuoteApproval.Update(approval);

                // for add requests, mark the quote approved
                if (approval.Type == Database.Models.QuoteApprovalType.AddRequest)
                {
                    quote.Approved = true;
                    db.Quotes.Update(quote);
                }

                await db.SaveChangesAsync();

                // notify the channel where the quote was submitted (if available)
                try
                {
                    if (channel is IMessageChannel msgCh)
                        await msgCh.SendMessageAsync($"Quote #{quote.Id} has been approved.");
                }
                catch { }
            }
        }
        catch
        {
            // swallow errors to avoid crashing the event
        }
    }
}
