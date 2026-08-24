using Discord;
using Discord.Commands;

namespace Morpheus.Extensions;

public abstract class MorpheusModuleBase : ModuleBase<SocketCommandContextExtended>
{
    protected new virtual Task<IUserMessage> ReplyAsync(
        string? message = null,
        bool isTTS = false,
        Embed? embed = null,
        RequestOptions? options = null,
        AllowedMentions? allowedMentions = null,
        MessageReference? messageReference = null,
        MessageComponent? components = null,
        ISticker[]? stickers = null,
        Embed[]? embeds = null,
        MessageFlags flags = MessageFlags.None) =>
        Context.SendResponseAsync(message, isTTS, embed, options, allowedMentions, messageReference, components, stickers, embeds, flags);

    protected Task<IUserMessage> SendFileResponseAsync(
        Stream stream,
        string filename,
        string? message = null,
        bool isTTS = false,
        Embed? embed = null,
        RequestOptions? options = null,
        bool isSpoiler = false,
        AllowedMentions? allowedMentions = null,
        MessageReference? messageReference = null,
        MessageComponent? components = null,
        ISticker[]? stickers = null,
        Embed[]? embeds = null,
        MessageFlags flags = MessageFlags.None) =>
        Context.SendFileResponseAsync(stream, filename, message, isTTS, embed, options, isSpoiler, allowedMentions, messageReference, components, stickers, embeds, flags);

    protected IDisposable EnterTypingState(RequestOptions? options = null) => Context.EnterTypingState(options);
    protected Task DeleteInvocationAsync(RequestOptions? options = null) => Context.DeleteInvocationAsync(options);
    protected Task AddInvocationReactionAsync(IEmote emote, RequestOptions? options = null) => Context.AddInvocationReactionAsync(emote, options);
}
