using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Morpheus.Attributes;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Services;

namespace Morpheus.MCP;

public sealed class McpCommandExecutionService : IDisposable
{
    private readonly CommandService commands;
    private readonly DiscordSocketClient client;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly McpApiOptions options;
    private readonly McpCommandCatalog catalog;
    private readonly ILogger<McpCommandExecutionService> logger;
    private readonly SemaphoreSlim executionGate;
    private readonly ConcurrentDictionary<ICommandContext, PendingCommand> pending =
        new(ReferenceEqualityComparer.Instance);
    private readonly ConcurrentDictionary<string, IdempotentExecution> idempotentExecutions = new(StringComparer.Ordinal);
    private bool disposed;

    internal Func<McpCommandInvocation, IServiceProvider, McpCommandResponseSink, bool, CancellationToken, Task<SocketCommandContextExtended>>?
        ContextFactoryOverride
    { get; init; }

    public McpCommandExecutionService(
        CommandService commands,
        DiscordSocketClient client,
        IServiceScopeFactory scopeFactory,
        McpApiOptions options,
        McpCommandCatalog catalog,
        ILogger<McpCommandExecutionService> logger)
    {
        this.commands = commands;
        this.client = client;
        this.scopeFactory = scopeFactory;
        this.options = options;
        this.catalog = catalog;
        this.logger = logger;
        executionGate = new SemaphoreSlim(options.MaxConcurrentCommands, options.MaxConcurrentCommands);
        commands.CommandExecuted += OnCommandExecutedAsync;
    }

    public Task<McpCommandExecutionResult> InvokeAsync(
        McpCommandInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(invocation);
        string mode = invocation.Mode.Trim().ToLowerInvariant();
        return mode switch
        {
            "validate" => ValidateAsync(invocation, cancellationToken),
            "execute" => ExecuteIdempotentlyAsync(invocation, cancellationToken),
            _ => throw new ArgumentException("mode must be validate or execute.", nameof(invocation))
        };
    }

    private async Task<McpCommandExecutionResult> ValidateAsync(
        McpCommandInvocation invocation,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string requestId = Guid.NewGuid().ToString("N");
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        McpCommandResponseSink sink = new(options, forwardToDiscord: false);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            SocketCommandContextExtended context = await CreateContextAsync(
                invocation,
                scope.ServiceProvider,
                sink,
                isValidation: true,
                cancellationToken);

            SearchResult search = commands.Search(invocation.Command.Trim());
            if (!search.IsSuccess || !search.Commands.Any(IsVisible))
                return Result(requestId, "validate", "invalid", false, false, false, null,
                    CommandError.UnknownCommand.ToString(), "Unknown command.", sink, stopwatch);

            IResult validation = await commands.ValidateAndGetBestMatch(
                search,
                context,
                scope.ServiceProvider,
                MultiMatchHandling.Best);
            cancellationToken.ThrowIfCancellationRequested();

            if (validation is not MatchResult match || match.Match is null || !IsVisible(match.Match.Value))
                return Result(requestId, "validate", "invalid", false, false, false, null,
                    validation.Error?.ToString(), SafeReason(validation), sink, stopwatch);

            IResult pipeline = match.Pipeline;
            McpCommandCapability? capability = catalog.FindByAlias(match.Match.Value.Alias);
            return Result(
                requestId,
                "validate",
                pipeline.IsSuccess ? "valid" : "invalid",
                pipeline.IsSuccess,
                false,
                false,
                capability,
                pipeline.Error?.ToString(),
                SafeReason(pipeline),
                sink,
                stopwatch);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP command validation {RequestId} failed before dispatch", requestId);
            return Result(requestId, "validate", "invalid-context", false, false, false, null,
                "InvalidContext", ex is ArgumentException or InvalidOperationException ? ex.Message : "Unable to resolve the Discord command context.", sink, stopwatch);
        }
    }

    private async Task<McpCommandExecutionResult> ExecuteIdempotentlyAsync(
        McpCommandInvocation invocation,
        CancellationToken cancellationToken)
    {
        if (!options.CommandExecutionEnabled)
            throw new InvalidOperationException("MCP command execution is disabled. Set MCP_COMMAND_EXECUTION_ENABLED=true after reviewing the security guidance.");
        if (!catalog.HasReviewedRegistry)
            throw new InvalidOperationException("The live command registry has not been reviewed for MCP execution.");
        cancellationToken.ThrowIfCancellationRequested();

        string idempotencyKey = invocation.IdempotencyKey!;
        string userId = ParseSnowflake(invocation.UserId, nameof(invocation.UserId)).ToString();
        string cacheKey = $"{userId}:{idempotencyKey}";
        string payloadHash = HashInvocation(invocation);
        RemoveExpiredIdempotencyEntries();

        IdempotentExecution created = new(
            payloadHash,
            DateTime.UtcNow,
            new Lazy<Task<McpCommandExecutionResult>>(
                () => ExecuteCoreAsync(invocation),
                LazyThreadSafetyMode.ExecutionAndPublication));
        IdempotentExecution actual = idempotentExecutions.GetOrAdd(cacheKey, created);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actual.PayloadHash),
            Encoding.UTF8.GetBytes(payloadHash)))
        {
            throw new InvalidOperationException("The idempotency key was already used with a different command invocation.");
        }

        if (!actual.Result.IsValueCreated)
            cancellationToken.ThrowIfCancellationRequested();
        McpCommandExecutionResult result = await actual.Result.Value.WaitAsync(cancellationToken);
        if (!result.SideEffectsMayHaveOccurred && result.Status is "busy" or "rejected")
            idempotentExecutions.TryRemove(new KeyValuePair<string, IdempotentExecution>(cacheKey, actual));
        return ReferenceEquals(actual, created) ? result : result with { IdempotentReplay = true };
    }

    private async Task<McpCommandExecutionResult> ExecuteCoreAsync(McpCommandInvocation invocation)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string requestId = Guid.NewGuid().ToString("N");
        McpCommandResponseSink sink = new(
            options,
            invocation.ResponseMode.Equals("discord", StringComparison.OrdinalIgnoreCase));
        if (!await executionGate.WaitAsync(0))
        {
            return Result(requestId, "execute", "busy", false, false, false, null,
                "ConcurrencyLimit", "Too many MCP commands are already executing.", sink, stopwatch);
        }

        AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        SocketCommandContextExtended? context = null;
        bool cleanupDeferred = false;

        try
        {
            context = await CreateContextAsync(
                invocation,
                scope.ServiceProvider,
                sink,
                isValidation: false,
                CancellationToken.None);

            SearchResult search = commands.Search(invocation.Command.Trim());
            if (!search.IsSuccess || !search.Commands.Any(IsVisible))
                return Result(requestId, "execute", "rejected", false, false, false, null,
                    CommandError.UnknownCommand.ToString(), "Unknown command.", sink, stopwatch);

            TaskCompletionSource<CommandCompletion> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            PendingCommand registration = new(completion);
            if (!pending.TryAdd(context, registration))
                throw new InvalidOperationException("The command context is already executing.");

            IResult dispatch = await commands.ExecuteAsync(
                context,
                invocation.Command.Trim(),
                scope.ServiceProvider,
                MultiMatchHandling.Best);

            if (!dispatch.IsSuccess && !completion.Task.IsCompleted)
                completion.TrySetResult(new CommandCompletion(null, dispatch));

            using CancellationTokenSource timeoutCancellation = new();
            Task timeout = Task.Delay(TimeSpan.FromSeconds(options.CommandTimeoutSeconds), timeoutCancellation.Token);
            Task winner = await Task.WhenAny(completion.Task, timeout);
            if (winner != completion.Task && !completion.Task.IsCompleted)
            {
                cleanupDeferred = true;
                _ = CleanupWhenCompletedAsync(context, completion.Task, scope);
                logger.LogWarning(
                    "MCP command {RequestId} timed out after {TimeoutSeconds}s for user {UserId}",
                    requestId,
                    options.CommandTimeoutSeconds,
                    invocation.UserId);
                return Result(requestId, "execute", "timed-out", false, dispatch.IsSuccess, dispatch.IsSuccess, null,
                    "Timeout", "The command exceeded the MCP response deadline. It may still finish in the background; do not retry without the same idempotency key.", sink, stopwatch);
            }
            timeoutCancellation.Cancel();

            CommandCompletion completed = await completion.Task;
            McpCommandCapability? capability = completed.Command is null
                ? null
                : completed.Command.Aliases.Select(catalog.FindByAlias).FirstOrDefault(value => value is not null);
            bool bodyStarted = dispatch.IsSuccess || completed.Result.Error == CommandError.Exception;
            logger.LogInformation(
                "MCP command {RequestId} finished with {Status} in {ElapsedMilliseconds}ms for user {UserId}, guild {GuildId}, channel {ChannelId}",
                requestId,
                completed.Result.IsSuccess ? "success" : completed.Result.Error?.ToString(),
                stopwatch.ElapsedMilliseconds,
                invocation.UserId,
                invocation.GuildId,
                invocation.ChannelId);
            return Result(
                requestId,
                "execute",
                completed.Result.IsSuccess ? "completed" : "failed",
                completed.Result.IsSuccess,
                bodyStarted,
                bodyStarted,
                capability,
                completed.Result.Error?.ToString(),
                SafeReason(completed.Result),
                sink,
                stopwatch);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP command {RequestId} failed before completion", requestId);
            return Result(requestId, "execute", "rejected", false, false, false, null,
                "InvalidContext", ex is ArgumentException or InvalidOperationException ? ex.Message : "Unable to execute the command.", sink, stopwatch);
        }
        finally
        {
            if (!cleanupDeferred)
            {
                if (context is not null)
                    pending.TryRemove(context, out _);
                await scope.DisposeAsync();
                executionGate.Release();
            }
        }
    }

    private async Task<SocketCommandContextExtended> CreateContextAsync(
        McpCommandInvocation invocation,
        IServiceProvider services,
        McpCommandResponseSink sink,
        bool isValidation,
        CancellationToken cancellationToken)
    {
        if (ContextFactoryOverride is not null)
            return await ContextFactoryOverride(invocation, services, sink, isValidation, cancellationToken);

        if (client.ConnectionState != ConnectionState.Connected)
            throw new InvalidOperationException("Morpheus is not connected to Discord.");

        ulong userId = ParseSnowflake(invocation.UserId, nameof(invocation.UserId));
        ulong channelId = ParseSnowflake(invocation.ChannelId, nameof(invocation.ChannelId));
        ulong? guildId = ParseOptionalSnowflake(invocation.GuildId, nameof(invocation.GuildId));

        SocketChannel? socketChannel = client.GetChannel(channelId);
        if (socketChannel is not IMessageChannel channel)
            throw new ArgumentException("The channel is not visible to Morpheus.", nameof(invocation.ChannelId));

        SocketGuild? socketGuild = (socketChannel as SocketGuildChannel)?.Guild;
        if (guildId.HasValue && socketGuild?.Id != guildId.Value)
            throw new ArgumentException("The channel does not belong to the supplied guild.", nameof(invocation.GuildId));
        if (socketGuild is not null && !guildId.HasValue)
            throw new ArgumentException("guildId is required for a guild channel.", nameof(invocation.GuildId));

        SocketUser? discordUser = socketGuild?.GetUser(userId) ?? client.GetUser(userId);
        if (discordUser is null)
            throw new ArgumentException("The user is not visible in the supplied Discord context.", nameof(invocation.UserId));
        if (discordUser.IsBot)
            throw new ArgumentException("Bot users cannot invoke Morpheus text commands through MCP.", nameof(invocation.UserId));

        cancellationToken.ThrowIfCancellationRequested();
        IUserMessage message;
        ulong? sourceMessageId = ParseOptionalSnowflake(invocation.SourceMessageId, nameof(invocation.SourceMessageId));
        if (sourceMessageId.HasValue)
        {
            message = await channel.GetMessageAsync(
                sourceMessageId.Value,
                CacheMode.AllowDownload,
                new RequestOptions { CancelToken = cancellationToken }) as IUserMessage
                ?? throw new ArgumentException("The source message is not visible in the supplied channel.", nameof(invocation.SourceMessageId));
            if (message.Author.Id != userId)
                throw new ArgumentException("The source message author does not match userId.", nameof(invocation.UserId));
        }
        else
        {
            IUserMessage? referencedMessage = null;
            ulong? referencedMessageId = ParseOptionalSnowflake(invocation.ReplyToMessageId, nameof(invocation.ReplyToMessageId));
            if (referencedMessageId.HasValue)
            {
                referencedMessage = await channel.GetMessageAsync(
                    referencedMessageId.Value,
                    CacheMode.AllowDownload,
                    new RequestOptions { CancelToken = cancellationToken }) as IUserMessage;
                if (referencedMessage is null)
                    throw new ArgumentException("The referenced message is not visible in the supplied channel.", nameof(invocation.ReplyToMessageId));
            }

            ValidateAttachments(invocation.Attachments ?? []);
            message = McpDiscordMessageFactory.CreateInvocation(
                discordUser,
                channel,
                invocation.MessageContent ?? invocation.Command,
                invocation.Attachments ?? [],
                referencedMessage,
                sink);
        }

        User? dbUser = null;
        Guild? dbGuild = null;
        if (!isValidation)
        {
            UsersService usersService = services.GetRequiredService<UsersService>();
            dbUser = await usersService.TryGetCreateUser(discordUser);
            await usersService.TryUpdateUsername(discordUser, dbUser);
            if (socketGuild is not null)
                dbGuild = await services.GetRequiredService<GuildService>().TryGetCreateGuild(socketGuild);
        }

        return new SocketCommandContextExtended(
            client,
            message,
            dbGuild,
            dbUser,
            sink,
            isValidation,
            invocation.Locale,
            invocation.TimeZoneId);
    }

    private void ValidateRequest(McpCommandInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        if (string.IsNullOrWhiteSpace(invocation.Mode))
            throw new ArgumentException("mode must be validate or execute.", nameof(invocation));
        if (string.IsNullOrWhiteSpace(invocation.ResponseMode) ||
            (!invocation.ResponseMode.Equals("capture", StringComparison.OrdinalIgnoreCase) &&
             !invocation.ResponseMode.Equals("discord", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("responseMode must be capture or discord.", nameof(invocation));
        if (string.IsNullOrWhiteSpace(invocation.Command) || invocation.Command.Length > options.MaxCommandLength)
            throw new ArgumentException($"command must contain between 1 and {options.MaxCommandLength} characters.", nameof(invocation));
        if (ContainsDisallowedControlCharacter(invocation.Command))
            throw new ArgumentException("command contains an unsupported control character.", nameof(invocation));
        if (invocation.MessageContent is not null &&
            (invocation.MessageContent.Length > options.MaxCommandLength || ContainsDisallowedControlCharacter(invocation.MessageContent)))
            throw new ArgumentException($"messageContent must be at most {options.MaxCommandLength} characters and contain no unsupported control characters.", nameof(invocation));
        _ = ParseSnowflake(invocation.UserId, nameof(invocation.UserId));
        _ = ParseSnowflake(invocation.ChannelId, nameof(invocation.ChannelId));
        _ = ParseOptionalSnowflake(invocation.GuildId, nameof(invocation.GuildId));
        _ = ParseOptionalSnowflake(invocation.SourceMessageId, nameof(invocation.SourceMessageId));
        _ = ParseOptionalSnowflake(invocation.ReplyToMessageId, nameof(invocation.ReplyToMessageId));
        ValidateAttachments(invocation.Attachments ?? []);
        ValidateLocaleAndTimeZone(invocation.Locale, invocation.TimeZoneId);

        if (invocation.Mode.Equals("execute", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(invocation.IdempotencyKey) || invocation.IdempotencyKey.Length is < 16 or > 128)
                throw new ArgumentException("execute mode requires an idempotencyKey containing between 16 and 128 characters.", nameof(invocation));
            if (invocation.IdempotencyKey.Any(char.IsControl))
                throw new ArgumentException("idempotencyKey cannot contain control characters.", nameof(invocation));
            if (string.IsNullOrWhiteSpace(invocation.SourceMessageId))
                throw new ArgumentException("execute mode requires sourceMessageId so Morpheus can verify the invoking Discord user and message context.", nameof(invocation));
        }
    }

    internal void ValidateAttachments(IReadOnlyList<McpCommandAttachment> attachments)
    {
        if (attachments.Count > options.MaxAttachments)
            throw new ArgumentException($"At most {options.MaxAttachments} attachments are allowed.", nameof(attachments));

        foreach (McpCommandAttachment? attachment in attachments)
        {
            if (attachment is null)
                throw new ArgumentException("Attachments cannot contain null entries.", nameof(attachments));
            if (string.IsNullOrWhiteSpace(attachment.Filename) || attachment.Filename.Length > 255 ||
                attachment.Filename is "." or ".." ||
                attachment.Filename.IndexOfAny(['/', '\\']) >= 0)
                throw new ArgumentException("Attachment filenames must be plain names containing at most 255 characters.", nameof(attachments));
            if (string.IsNullOrWhiteSpace(attachment.Url) || attachment.Url.Length > 2048)
                throw new ArgumentException("Attachment URLs must contain at most 2048 characters.", nameof(attachments));
            if (attachment.ContentType?.Length > 255)
                throw new ArgumentException("Attachment content types must contain at most 255 characters.", nameof(attachments));
            if (attachment.Description?.Length > 1024)
                throw new ArgumentException("Attachment descriptions must contain at most 1024 characters.", nameof(attachments));
            if (attachment.Size is < 0 || attachment.Size > 0 && attachment.Size > options.MaxAttachmentBytes)
                throw new ArgumentException($"Attachments must be no larger than {options.MaxAttachmentBytes} bytes.", nameof(attachments));
            if (!Uri.TryCreate(attachment.Url, UriKind.Absolute, out Uri? uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                (uri.Host != "cdn.discordapp.com" && uri.Host != "media.discordapp.net"))
                throw new ArgumentException("Attachment URLs must use Discord's HTTPS CDN.", nameof(attachments));
        }
    }

    private Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
    {
        if (pending.TryGetValue(context, out PendingCommand? registration))
            registration.Completion.TrySetResult(new CommandCompletion(command.IsSpecified ? command.Value : null, result));
        return Task.CompletedTask;
    }

    private async Task CleanupWhenCompletedAsync(
        ICommandContext context,
        Task<CommandCompletion> completion,
        AsyncServiceScope scope)
    {
        try
        {
            await completion;
        }
        finally
        {
            pending.TryRemove(context, out _);
            await scope.DisposeAsync();
            executionGate.Release();
        }
    }

    private static bool IsVisible(CommandMatch match) =>
        !match.Command.Attributes.OfType<HiddenAttribute>().Any();

    private static string? SafeReason(IResult result) =>
        result.Error == CommandError.Exception ? "Command execution failed." : result.ErrorReason;

    private static McpCommandExecutionResult Result(
        string requestId,
        string mode,
        string status,
        bool success,
        bool executed,
        bool sideEffects,
        McpCommandCapability? command,
        string? error,
        string? errorReason,
        McpCommandResponseSink sink,
        Stopwatch stopwatch) => new(
            requestId,
            mode,
            status,
            success,
            executed,
            sideEffects,
            IdempotentReplay: false,
            command,
            error,
            errorReason,
            sink.Snapshot(),
            stopwatch.ElapsedMilliseconds);

    private static ulong ParseSnowflake(string value, string name) =>
        value is { Length: > 0 and <= 20 } && ulong.TryParse(value, out ulong parsed) && parsed > 0
            ? parsed
            : throw new ArgumentException($"{name} must be a positive decimal Discord id.", name);

    private static ulong? ParseOptionalSnowflake(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseSnowflake(value, name);

    private static string HashInvocation(McpCommandInvocation invocation)
    {
        bool usesSourceMessage = !string.IsNullOrWhiteSpace(invocation.SourceMessageId);
        string canonical = string.Join('\n',
            invocation.Command.Trim(),
            invocation.UserId,
            invocation.ChannelId,
            invocation.GuildId ?? string.Empty,
            invocation.SourceMessageId ?? string.Empty,
            usesSourceMessage ? string.Empty : invocation.MessageContent ?? string.Empty,
            usesSourceMessage ? string.Empty : invocation.ReplyToMessageId ?? string.Empty,
            invocation.ResponseMode.ToLowerInvariant(),
            invocation.Locale ?? string.Empty,
            invocation.TimeZoneId ?? string.Empty,
            usesSourceMessage ? string.Empty : string.Join('|', (invocation.Attachments ?? []).Select(attachment =>
                $"{attachment.Filename}:{attachment.Url}:{attachment.Size}:{attachment.ContentType}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static bool ContainsDisallowedControlCharacter(string value) =>
        value.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t');

    private static void ValidateLocaleAndTimeZone(string? locale, string? timeZoneId)
    {
        if (locale is not null)
        {
            if (string.IsNullOrWhiteSpace(locale) || locale.Length > 35)
                throw new ArgumentException("locale must be a recognized culture name containing at most 35 characters.", nameof(locale));
            try
            {
                CultureInfo culture = CultureInfo.GetCultureInfo(locale);
                if (!CultureInfo.GetCultures(CultureTypes.AllCultures)
                    .Any(candidate => candidate.Name.Equals(culture.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new CultureNotFoundException(nameof(locale), locale, "The culture name is not installed.");
            }
            catch (CultureNotFoundException ex)
            {
                throw new ArgumentException("locale must be a recognized culture name.", nameof(locale), ex);
            }
        }

        if (timeZoneId is not null)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Length > 100)
                throw new ArgumentException("timeZoneId must be a recognized time-zone id containing at most 100 characters.", nameof(timeZoneId));
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException ex)
            {
                throw new ArgumentException("timeZoneId must be a recognized time-zone id.", nameof(timeZoneId), ex);
            }
            catch (InvalidTimeZoneException ex)
            {
                throw new ArgumentException("timeZoneId refers to invalid system time-zone data.", nameof(timeZoneId), ex);
            }
        }
    }

    private void RemoveExpiredIdempotencyEntries()
    {
        DateTime cutoff = DateTime.UtcNow.AddMinutes(-options.IdempotencyMinutes);
        foreach ((string key, IdempotentExecution value) in idempotentExecutions)
        {
            if (value.CreatedAtUtc < cutoff && value.Result.IsValueCreated && value.Result.Value.IsCompleted)
                idempotentExecutions.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        commands.CommandExecuted -= OnCommandExecutedAsync;
    }

    private sealed record PendingCommand(TaskCompletionSource<CommandCompletion> Completion);
    private sealed record CommandCompletion(CommandInfo? Command, IResult Result);
    private sealed record IdempotentExecution(
        string PayloadHash,
        DateTime CreatedAtUtc,
        Lazy<Task<McpCommandExecutionResult>> Result);
}
