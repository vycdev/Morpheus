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
    private readonly LogsService logsService;
    private readonly bool started = false;

    public MessagesHandler(DiscordSocketClient client, CommandService commands, IServiceProvider serviceProvider, GuildService guildService, UsersService usersService, LogsService logsService)
    {
        if (started)
            throw new InvalidOperationException("At most one instance of this service can be started");

        started = true;

        this.client = client;
        this.commands = commands;
        this.serviceProvider = serviceProvider;
        this.guildService = guildService;
        this.usersService = usersService;
        this.logsService = logsService;

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
        // Only process the up-arrow emoji used for approvals
        if (reaction.Emote is not Emoji emote || emote.Name != "⬆️")
            return;

        var messageId = reaction.MessageId;

        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = (Database.DB)scope.ServiceProvider.GetRequiredService(typeof(Database.DB));

            var approval = await db.QuoteApprovals.FirstOrDefaultAsync(a => a.ApprovalMessageId == (ulong)messageId);
            if (approval == null) return; // not an approval message
            if (approval.Approved) return; // already finalized

            // Recompute score from message reactions
            var cachedMessage = await cache.GetOrDownloadAsync();
            var newScore = 0;
            if (cachedMessage != null && cachedMessage.Reactions.TryGetValue(new Emoji("⬆️"), out var reactionMeta))
            {
                newScore = (int)reactionMeta.ReactionCount;
                try { if (reactionMeta.IsMe) newScore -= 1; }
                catch (Exception ex)
                {
                    logsService.Log($"Error while computing reaction metadata: {ex}", LogSeverity.Error);
                }
                if (newScore < 0) newScore = 0;
            }

            approval.Score = newScore;
            await db.SaveChangesAsync();

            var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == approval.QuoteId);
            if (quote == null) return;
            var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Id == quote.GuildId);
            if (guild == null) return;

            // Live-update: replace or append the Approvals line in the existing message content
            IUserMessage? approvalMessage = await cache.GetOrDownloadAsync();
            if (approvalMessage != null)
            {
                try
                {
                    var required = approval.Type == Database.Models.QuoteApprovalType.AddRequest ? guild.QuoteAddRequiredApprovals : guild.QuoteRemoveRequiredApprovals;
                    var content = approvalMessage.Content ?? string.Empty;
                    var lines = content.Split('\n').ToList();
                    var idx = lines.FindLastIndex(l => l.TrimStart().StartsWith("Approvals:", StringComparison.OrdinalIgnoreCase));
                    var approvalsLine = $"Approvals: {approval.Score} / {required}";
                    if (idx >= 0) lines[idx] = approvalsLine; else lines.Add(approvalsLine);
                    var newContent = string.Join('\n', lines);
                    await approvalMessage.ModifyAsync(m => m.Content = newContent);
                }
                catch (Exception ex)
                {
                    logsService.Log($"Error updating approval message content: {ex}", LogSeverity.Error);
                }
            }

            var requiredApprovals = approval.Type == Database.Models.QuoteApprovalType.AddRequest ? guild.QuoteAddRequiredApprovals : guild.QuoteRemoveRequiredApprovals;
            if (!approval.Approved && approval.Score >= requiredApprovals)
            {
                approval.Approved = true;

                if (approval.Type == Database.Models.QuoteApprovalType.AddRequest)
                    quote.Approved = true;
                else if (approval.Type == Database.Models.QuoteApprovalType.RemoveRequest)
                    quote.Removed = true;

                await db.SaveChangesAsync();

                // Finalize: remove Approvals line and prefix with APPROVED/REMOVED while preserving body
                try
                {
                    approvalMessage ??= await cache.GetOrDownloadAsync();
                    if (approvalMessage != null)
                    {
                        var orig = approvalMessage.Content ?? string.Empty;
                        var lines = orig.Split('\n').Where(l => !l.TrimStart().StartsWith("Approvals:", StringComparison.OrdinalIgnoreCase)).ToList();
                        // Find the fenced code block lines (```...```) and extract quote body if present
                        var body = string.Join('\n', lines);
                        // Construct a nicer final message
                        string finalContent;
                        if (approval.Type == Database.Models.QuoteApprovalType.AddRequest)
                            finalContent = $"✅ **ADD APPROVED — Quote #{quote.Id}**\n\n{body}\nFinal approvals: {approval.Score} / {requiredApprovals}";
                        else
                            finalContent = $"🗑️ **REMOVAL APPROVED — Quote #{quote.Id}**\n\n{body}\nFinal approvals: {approval.Score} / {requiredApprovals}";

                        await approvalMessage.ModifyAsync(m => m.Content = finalContent);
                        try { await approvalMessage.RemoveAllReactionsAsync(); }
                        catch (Exception ex)
                        {
                            logsService.Log($"Failed to remove reactions from approval message: {ex}", LogSeverity.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logsService.Log($"Error finalizing approval message: {ex}", LogSeverity.Error);
                }
            }
        }
        catch (Exception ex)
        {
            logsService.Log($"Error processing reaction change: {ex}", LogSeverity.Error);
        }
    }
}
