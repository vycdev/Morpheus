using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using Discord;
using Morpheus.Extensions;

namespace Morpheus.MCP;

internal static class McpDiscordMessageFactory
{
    private static long nextMessageSequence;

    public static IUserMessage CreateInvocation(
        IUser author,
        IMessageChannel channel,
        string content,
        IReadOnlyList<McpCommandAttachment> attachments,
        IUserMessage? referencedMessage,
        ICommandResponseSink sink)
    {
        ulong id = NewSnowflake();
        IReadOnlyCollection<IAttachment> discordAttachments = [.. attachments.Select(CreateAttachment)];
        ulong[] mentionedUsers = ParseMentionIds(content, "<@", trimPrefixCharacters: "!");
        ulong[] mentionedRoles = ParseMentionIds(content, "<@&", trimPrefixCharacters: string.Empty);
        ulong[] mentionedChannels = ParseMentionIds(content, "<#", trimPrefixCharacters: string.Empty);

        return DiscordProxy.Create<IUserMessage>((method, args) => method.Name switch
        {
            "get_Id" => id,
            "get_Content" or "get_CleanContent" => content,
            "get_Author" => author,
            "get_Channel" => channel,
            "get_Timestamp" or "get_CreatedAt" => DateTimeOffset.UtcNow,
            "get_Attachments" => discordAttachments,
            "get_ReferencedMessage" => referencedMessage,
            "get_MentionedUserIds" => mentionedUsers,
            "get_MentionedRoleIds" => mentionedRoles,
            "get_MentionedChannelIds" => mentionedChannels,
            "get_Type" => MessageType.Default,
            "get_Source" => MessageSource.User,
            "Resolve" => content,
            "DeleteAsync" => sink.RecordInvocationEffectAsync("delete-invocation"),
            "AddReactionAsync" => sink.RecordInvocationEffectAsync("add-invocation-reaction", args?[0]?.ToString()),
            "ModifyAsync" => RecordModification(sink, args),
            "PinAsync" => sink.RecordInvocationEffectAsync("pin-invocation"),
            "UnpinAsync" => sink.RecordInvocationEffectAsync("unpin-invocation"),
            "CrosspostAsync" => sink.RecordInvocationEffectAsync("crosspost-invocation"),
            "EndPollAsync" => sink.RecordInvocationEffectAsync("end-invocation-poll"),
            "ToString" => content,
            _ => DiscordProxy.DefaultValue(method.ReturnType)
        });
    }

    public static IUserMessage CreateCapturedResponse(
        IUser author,
        IMessageChannel channel,
        string content,
        McpCommandResponseSink sink)
    {
        ulong id = NewSnowflake();
        return DiscordProxy.Create<IUserMessage>((method, args) => method.Name switch
        {
            "get_Id" => id,
            "get_Content" or "get_CleanContent" => content,
            "get_Author" => author,
            "get_Channel" => channel,
            "get_Timestamp" or "get_CreatedAt" => DateTimeOffset.UtcNow,
            "get_Type" => MessageType.Default,
            "get_Source" => MessageSource.Bot,
            "Resolve" => content,
            "ModifyAsync" => sink.RecordResponseModificationAsync(args),
            "DeleteAsync" => sink.RecordInvocationEffectAsync("delete-captured-response", id.ToString()),
            "AddReactionAsync" => sink.RecordInvocationEffectAsync("add-captured-response-reaction", args?[0]?.ToString()),
            "PinAsync" => sink.RecordInvocationEffectAsync("pin-captured-response", id.ToString()),
            "UnpinAsync" => sink.RecordInvocationEffectAsync("unpin-captured-response", id.ToString()),
            "ToString" => content,
            _ => DiscordProxy.DefaultValue(method.ReturnType)
        });
    }

    public static IUserMessage CreateForwardedResponse(
        IUserMessage message,
        McpCommandResponseSink sink) =>
        DiscordProxy.Create<IUserMessage>((method, args) =>
        {
            if (method.Name == "ModifyAsync" && args?.FirstOrDefault() is Action<MessageProperties> update)
            {
                args[0] = new Action<MessageProperties>(properties =>
                {
                    update(properties);
                    sink.RecordResponseModification(properties);
                });
            }
            return method.Invoke(message, args);
        });

    private static IAttachment CreateAttachment(McpCommandAttachment attachment) =>
        DiscordProxy.Create<IAttachment>((method, _) => method.Name switch
        {
            "get_Id" => NewSnowflake(),
            "get_Filename" => attachment.Filename,
            "get_Url" or "get_ProxyUrl" => attachment.Url,
            "get_Size" => (int)Math.Min(attachment.Size, int.MaxValue),
            "get_ContentType" => attachment.ContentType,
            "get_Description" => attachment.Description,
            "get_CreatedAt" => DateTimeOffset.UtcNow,
            "ToString" => attachment.Url,
            _ => DiscordProxy.DefaultValue(method.ReturnType)
        });

    private static Task RecordModification(ICommandResponseSink sink, object?[]? args)
    {
        if (args?.FirstOrDefault() is Action<MessageProperties> update)
        {
            MessageProperties properties = new();
            update(properties);
        }

        return sink.RecordInvocationEffectAsync("modify-invocation");
    }

    private static ulong[] ParseMentionIds(string content, string prefix, string trimPrefixCharacters)
    {
        List<ulong> ids = [];
        int position = 0;
        while ((position = content.IndexOf(prefix, position, StringComparison.Ordinal)) >= 0)
        {
            int start = position + prefix.Length;
            while (start < content.Length && trimPrefixCharacters.Contains(content[start]))
                start++;
            int end = content.IndexOf('>', start);
            if (end < 0)
                break;
            if (ulong.TryParse(content.AsSpan(start, end - start), out ulong id) && id > 0)
                ids.Add(id);
            position = end + 1;
        }

        return [.. ids.Distinct()];
    }

    private static ulong NewSnowflake()
    {
        ulong timestamp = SnowflakeUtils.ToSnowflake(DateTimeOffset.UtcNow);
        ulong sequence = (ulong)(Interlocked.Increment(ref nextMessageSequence) & 0x3fffff);
        return timestamp | sequence;
    }
}

internal class DiscordProxy : DispatchProxy
{
    private Func<MethodInfo, object?[]?, object?>? handler;

    public static T Create<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        T value = Create<T, DiscordProxy>();
        ((DiscordProxy)(object)value).handler = handler;
        return value;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        handler!(targetMethod!, args);

    public static object? DefaultValue(Type type)
    {
        if (type == typeof(void))
            return null;
        if (type == typeof(Task))
            return Task.CompletedTask;
        if (type == typeof(ValueTask))
            return ValueTask.CompletedTask;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            Type resultType = type.GetGenericArguments()[0];
            MethodInfo fromResult = typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType);
            return fromResult.Invoke(null, [resultType.IsValueType ? Activator.CreateInstance(resultType) : null]);
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
        {
            MethodInfo empty = typeof(DiscordProxy).GetMethod(nameof(EmptyAsync), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(type.GetGenericArguments()[0]);
            return empty.Invoke(null, null);
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>))
            return Array.CreateInstance(type.GetGenericArguments()[0], 0);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
            return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments()));
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static async IAsyncEnumerable<T> EmptyAsync<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}

internal sealed class McpCommandResponseSink(
    McpApiOptions options,
    bool forwardToDiscord = false) : ICommandResponseSink
{
    private readonly ConcurrentQueue<McpCapturedOutput> outputs = new();
    private int sequence;
    private int truncationNoticeAdded;
    private long remainingFileBytes = options.MaxCapturedOutputBytes;

    public IReadOnlyList<McpCapturedOutput> Snapshot() => [.. outputs.OrderBy(output => output.Sequence)];

    public Task<IUserMessage> SendAsync(
        SocketCommandContextExtended context,
        string? message,
        bool isTTS,
        Embed? embed,
        AllowedMentions? allowedMentions,
        MessageReference? messageReference,
        MessageComponent? components,
        ISticker[]? stickers,
        Embed[]? embeds,
        MessageFlags flags)
    {
        List<Embed> allEmbeds = [];
        if (embed is not null)
            allEmbeds.Add(embed);
        if (embeds is not null)
            allEmbeds.AddRange(embeds);

        Add("message", message, allEmbeds, null, components is null ? null : $"components:{components.Components.Count}");
        if (forwardToDiscord)
            return SendToDiscordAsync();

        IUser author = context.Client.CurrentUser ?? context.User;
        return Task.FromResult(McpDiscordMessageFactory.CreateCapturedResponse(author, context.Channel, message ?? string.Empty, this));

        async Task<IUserMessage> SendToDiscordAsync()
        {
            IUserMessage sent = await context.Channel.SendMessageAsync(
                message,
                isTTS,
                embed,
                allowedMentions: allowedMentions,
                messageReference: messageReference,
                components: components,
                stickers: stickers,
                embeds: embeds,
                flags: flags);
            return McpDiscordMessageFactory.CreateForwardedResponse(sent, this);
        }
    }

    public async Task<IUserMessage> SendFileAsync(
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
        MessageFlags flags)
    {
        long originalPosition = stream.CanSeek ? stream.Position : 0;
        McpCapturedFile file = await CaptureFileAsync(stream, filename);
        List<Embed> allEmbeds = [];
        if (embed is not null)
            allEmbeds.Add(embed);
        if (embeds is not null)
            allEmbeds.AddRange(embeds);
        Add("file", message, allEmbeds, file, isSpoiler ? "spoiler" : null);
        if (forwardToDiscord)
        {
            if (!stream.CanSeek)
                throw new InvalidOperationException("Discord response forwarding requires a seekable file stream.");
            stream.Position = originalPosition;
            IUserMessage sent = await context.Channel.SendFileAsync(
                stream,
                filename,
                message,
                isTTS,
                embed,
                isSpoiler: isSpoiler,
                allowedMentions: allowedMentions,
                messageReference: messageReference,
                components: components,
                stickers: stickers,
                embeds: embeds,
                flags: flags);
            return McpDiscordMessageFactory.CreateForwardedResponse(sent, this);
        }

        IUser author = context.Client.CurrentUser ?? context.User;
        return McpDiscordMessageFactory.CreateCapturedResponse(author, context.Channel, message ?? string.Empty, this);
    }

    public IDisposable EnterTypingState()
    {
        Add("typing", null, [], null, null);
        return NoopDisposable.Instance;
    }

    public Task RecordInvocationEffectAsync(string kind, string? detail = null)
    {
        Add(kind, null, [], null, detail);
        return Task.CompletedTask;
    }

    public Task RecordResponseModificationAsync(object?[]? args)
    {
        if (args?.FirstOrDefault() is Action<MessageProperties> update)
        {
            MessageProperties properties = new();
            update(properties);
            RecordResponseModification(properties);
        }

        return Task.CompletedTask;
    }

    public void RecordResponseModification(MessageProperties properties) =>
        Add(
            "modify-response",
            ReadOptional<string>(properties, nameof(MessageProperties.Content)),
            [],
            null,
            null);

    private async Task<McpCapturedFile> CaptureFileAsync(Stream stream, string filename)
    {
        int limit = (int)Math.Min(options.MaxCapturedOutputBytes, Math.Max(0, Interlocked.Read(ref remainingFileBytes)));
        long? knownLength = stream.CanSeek ? stream.Length - stream.Position : null;
        if (knownLength > limit)
            return new McpCapturedFile(filename, knownLength.Value, null, null, null, Truncated: true);

        using MemoryStream buffer = new();
        byte[] chunk = new byte[81920];
        bool truncated = false;
        while (buffer.Length <= limit)
        {
            int toRead = (int)Math.Min(chunk.Length, limit + 1L - buffer.Length);
            int read = await stream.ReadAsync(chunk.AsMemory(0, toRead));
            if (read == 0)
                break;
            await buffer.WriteAsync(chunk.AsMemory(0, read));
            if (buffer.Length > limit)
            {
                truncated = true;
                break;
            }
        }

        byte[] data = buffer.ToArray();
        if (truncated)
            return new McpCapturedFile(filename, data.LongLength, null, null, null, Truncated: true);
        if (Interlocked.Add(ref remainingFileBytes, -data.LongLength) < 0)
        {
            Interlocked.Add(ref remainingFileBytes, data.LongLength);
            return new McpCapturedFile(filename, data.LongLength, null, null, null, Truncated: true);
        }

        return new McpCapturedFile(
            filename,
            data.LongLength,
            Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(),
            null,
            Convert.ToBase64String(data),
            Truncated: false);
    }

    private void Add(string kind, string? content, IEnumerable<Embed> embeds, McpCapturedFile? file, string? detail)
    {
        int current = Interlocked.Increment(ref sequence);
        if (current > options.MaxCapturedOutputs)
        {
            if (Interlocked.Exchange(ref truncationNoticeAdded, 1) == 0)
            {
                outputs.Enqueue(new McpCapturedOutput(
                    current,
                    "output-limit",
                    null,
                    [],
                    null,
                    $"Additional outputs were omitted after {options.MaxCapturedOutputs} items."));
            }
            return;
        }

        if (content?.Length > 8000)
        {
            int prefixLength = 8000;
            if (char.IsHighSurrogate(content[prefixLength - 1]) &&
                char.IsLowSurrogate(content[prefixLength]))
            {
                prefixLength--;
            }

            content = string.Concat(content.AsSpan(0, prefixLength), "…");
        }
        outputs.Enqueue(new McpCapturedOutput(
            current,
            kind,
            content,
            [.. embeds.Select(CaptureEmbed)],
            file,
            detail));
    }

    private static McpCapturedEmbed CaptureEmbed(Embed embed) => new(
        embed.Title,
        embed.Description,
        embed.Url,
        embed.Author?.Name,
        embed.Author?.IconUrl,
        embed.Footer?.Text,
        embed.Footer?.IconUrl,
        embed.Image?.Url,
        embed.Thumbnail?.Url,
        embed.Color?.RawValue,
        embed.Timestamp,
        [.. embed.Fields.Select(field => new McpCapturedEmbedField(field.Name, field.Value, field.Inline))]);

    private static T? ReadOptional<T>(object source, string name)
    {
        object? optional = source.GetType().GetProperty(name)?.GetValue(source);
        if (optional is null)
            return default;
        Type type = optional.GetType();
        if (type.GetProperty("IsSpecified")?.GetValue(optional) is not true)
            return default;
        return type.GetProperty("Value")?.GetValue(optional) is T value ? value : default;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();
        public void Dispose() { }
    }
}
