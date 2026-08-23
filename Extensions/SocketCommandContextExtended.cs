using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Database.Models;

namespace Morpheus.Extensions;

public class SocketCommandContextExtended(
    DiscordSocketClient client,
    IUserMessage msg,
    Guild? guild,
    User? user,
    ICommandResponseSink? responseSink = null,
    bool isValidation = false,
    string? locale = null,
    string? timeZoneId = null
) : CommandContext(client, msg)
{
    public new DiscordSocketClient Client { get; } = client;
    public new SocketGuild Guild { get; } = (msg.Channel as SocketGuildChannel)?.Guild!;
    public new IMessageChannel Channel { get; } = msg.Channel;
    public new IUser User { get; } = msg.Author;
    public new IUserMessage Message { get; } = msg;
    public Guild? DbGuild { get; set; } = guild;
    public User? DbUser { get; set; } = user;
    public ICommandResponseSink? ResponseSink { get; } = responseSink;
    public bool IsValidation { get; } = isValidation;
    public string? Locale { get; } = locale;
    public string? TimeZoneId { get; } = timeZoneId;

    public Task<IUserMessage> SendResponseAsync(
        string? message = null,
        bool isTTS = false,
        Embed? embed = null,
        RequestOptions? options = null,
        AllowedMentions? allowedMentions = null,
        MessageReference? messageReference = null,
        MessageComponent? components = null,
        ISticker[]? stickers = null,
        Embed[]? embeds = null,
        MessageFlags flags = MessageFlags.None)
    {
        if (ResponseSink is not null)
            return ResponseSink.SendAsync(this, message, isTTS, embed, allowedMentions, messageReference, components, stickers, embeds, flags);

        return Channel.SendMessageAsync(message, isTTS, embed, options, allowedMentions, messageReference, components, stickers, embeds, flags);
    }

    public Task<IUserMessage> SendFileResponseAsync(
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
        MessageFlags flags = MessageFlags.None)
    {
        if (ResponseSink is not null)
            return ResponseSink.SendFileAsync(this, stream, filename, message, isTTS, embed, isSpoiler, allowedMentions, messageReference, components, stickers, embeds, flags);

        return Channel.SendFileAsync(stream, filename, message, isTTS, embed, options, isSpoiler, allowedMentions, messageReference, components, stickers, embeds, flags);
    }

    public IDisposable EnterTypingState(RequestOptions? options = null) =>
        ResponseSink?.EnterTypingState() ?? Channel.EnterTypingState(options);

    public Task DeleteInvocationAsync(RequestOptions? options = null) =>
        ResponseSink?.RecordInvocationEffectAsync("delete-invocation") ?? Message.DeleteAsync(options);

    public Task AddInvocationReactionAsync(IEmote emote, RequestOptions? options = null) =>
        ResponseSink?.RecordInvocationEffectAsync("add-invocation-reaction", emote.ToString()) ?? Message.AddReactionAsync(emote, options);
}

public interface ICommandResponseSink
{
    Task<IUserMessage> SendAsync(
        SocketCommandContextExtended context,
        string? message,
        bool isTTS,
        Embed? embed,
        AllowedMentions? allowedMentions,
        MessageReference? messageReference,
        MessageComponent? components,
        ISticker[]? stickers,
        Embed[]? embeds,
        MessageFlags flags);

    Task<IUserMessage> SendFileAsync(
        SocketCommandContextExtended context,
        Stream stream,
        string filename,
        string? message,
        bool isTTS,
        Embed? embed,
        bool isSpoiler,
        AllowedMentions? allowedMentions,
        MessageReference? messageReference,
        MessageComponent? components,
        ISticker[]? stickers,
        Embed[]? embeds,
        MessageFlags flags);

    IDisposable EnterTypingState();
    Task RecordInvocationEffectAsync(string kind, string? detail = null);
}
