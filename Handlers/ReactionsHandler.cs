using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database.Models;
using Morpheus.Services;
using System.Collections.Concurrent;

namespace Morpheus.Handlers;

public class ReactionsHandler
{
    private readonly DiscordSocketClient _client;
    private readonly IServiceProvider _services;
    private readonly LogsService _logs;
    // Per-message locks to serialize Modify/RemoveAll calls and avoid concurrent REST storms
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _messageLocks = new();

    public ReactionsHandler(DiscordSocketClient client, IServiceProvider services, LogsService logs)
    {
        _client = client;
        _services = services;
        _logs = logs;

        _client.ReactionAdded += OnReactionAdded;
        _client.ReactionRemoved += OnReactionRemoved;
    }


    private Task OnReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channelCache, SocketReaction reaction)
    {
        _ = Task.Run(() => ProcessReaction(cache, channelCache, reaction));
        return Task.CompletedTask;
    }

    private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channelCache, SocketReaction reaction)
    {
        _ = Task.Run(() => ProcessReaction(cache, channelCache, reaction));
        return Task.CompletedTask;
    }

    private async Task ProcessReaction(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channelCache, SocketReaction reaction)
    {
        // Only process the up-arrow emoji used for approvals
        if (reaction.Emote is not Emoji emote || emote.Name != "⬆️")
            return;

        var messageId = reaction.MessageId;
        var sem = _messageLocks.GetOrAdd(messageId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            using var scope = _services.CreateScope();
            var db = (Database.DB)scope.ServiceProvider.GetRequiredService(typeof(Database.DB));

            var approval = await db.QuoteApprovals.FirstOrDefaultAsync(a => a.ApprovalMessageId == messageId);
            if (approval == null) return;
            if (approval.Approved) return;

            // Fetch message once
            var msg = await cache.GetOrDownloadAsync();

            // Expiry handling
            var expiry = approval.InsertDate.AddDays(5);
            if (DateTime.UtcNow > expiry)
            {
                approval.Approved = false; // mark expired
                await db.SaveChangesAsync();

                if (msg != null)
                {
                    try
                    {
                        var orig = msg.Content ?? string.Empty;
                        var lines = orig.Split('\n').Where(l => !l.TrimStart().StartsWith("Approvals:", StringComparison.OrdinalIgnoreCase)).ToList();
                        var body = string.Join('\n', lines);
                        var finalContent = $"⏳ **APPROVAL EXPIRED — Quote #{approval.QuoteId}**\n\n{body}\nThis approval request expired after 5 days and can no longer be approved.";
                        if (msg.Content != finalContent)
                            await msg.ModifyAsync(m => m.Content = finalContent);
                        try { await msg.RemoveAllReactionsAsync(); }
                        catch (Exception ex) { _logs.Log($"Failed to remove reactions from expired approval message: {ex}", LogSeverity.Warning); }
                    }
                    catch (Exception ex)
                    {
                        _logs.Log($"Error finalizing expired approval message: {ex}", LogSeverity.Error);
                    }
                }

                return;
            }

            // Recompute score
            var newScore = 0;
            if (msg != null && msg.Reactions.TryGetValue(new Emoji("⬆️"), out var reactionMeta))
            {
                newScore = (int)reactionMeta.ReactionCount;
                try { if (reactionMeta.IsMe) newScore -= 1; }
                catch { }
                if (newScore < 0) newScore = 0;
            }

            approval.Score = newScore;
            await db.SaveChangesAsync();

            var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == approval.QuoteId);
            if (quote == null) return;
            var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Id == quote.GuildId);
            if (guild == null) return;

            var requiredApprovals = approval.Type == Database.Models.QuoteApprovalType.AddRequest ? guild.QuoteAddRequiredApprovals : guild.QuoteRemoveRequiredApprovals;
            if (!approval.Approved && approval.Score >= requiredApprovals)
            {
                approval.Approved = true;
                if (approval.Type == Database.Models.QuoteApprovalType.AddRequest)
                    quote.Approved = true;
                else
                    quote.Removed = true;

                await db.SaveChangesAsync();

                if (msg != null)
                {
                    try
                    {
                        var orig = msg.Content ?? string.Empty;
                        var lines = orig.Split('\n').Where(l => !l.TrimStart().StartsWith("Approvals:", StringComparison.OrdinalIgnoreCase)).ToList();
                        var body = string.Join('\n', lines);
                        string finalContent;
                        if (approval.Type == Database.Models.QuoteApprovalType.AddRequest)
                            finalContent = $"✅ **ADD APPROVED — Quote #{quote.Id}**\n\n{body}\nFinal approvals: {approval.Score} / {requiredApprovals}";
                        else
                            finalContent = $"🗑️ **REMOVAL APPROVED — Quote #{quote.Id}**\n\n{body}\nFinal approvals: {approval.Score} / {requiredApprovals}";

                        if (msg.Content != finalContent)
                            await msg.ModifyAsync(m => m.Content = finalContent);

                        try { await msg.RemoveAllReactionsAsync(); }
                        catch (Exception ex) { _logs.Log($"Failed to remove reactions from approval message: {ex}", LogSeverity.Warning); }
                    }
                    catch (Exception ex)
                    {
                        _logs.Log($"Error finalizing approval message: {ex}", LogSeverity.Error);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logs.Log($"Error processing reaction change: {ex}", LogSeverity.Error);
        }
        finally
        {
            sem.Release();
            _messageLocks.TryRemove(messageId, out var _);
        }
    }
}