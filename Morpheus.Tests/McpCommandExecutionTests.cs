using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.MCP;

namespace Morpheus.Tests;

public class McpCommandExecutionTests
{
    [Fact]
    public async Task Execute_UsesNormalParserCapturesOutputAndReplaysIdempotently()
    {
        await using TestHarness harness = await TestHarness.CreateAsync(executionEnabled: true);
        McpCommandInvocation invocation = new(
            "echo hello from MCP",
            harness.UserId.ToString(),
            harness.ChannelId.ToString(),
            SourceMessageId: "900000000000005",
            Mode: "execute",
            IdempotencyKey: "test-execution-key-0001");

        McpCommandExecutionResult first = await harness.Service.InvokeAsync(invocation);
        McpCommandExecutionResult replay = await harness.Service.InvokeAsync(invocation);

        Assert.True(first.Success, $"{first.Error}: {first.ErrorReason}");
        Assert.True(first.Executed);
        Assert.Equal("completed", first.Status);
        Assert.Equal("hello from MCP", Assert.Single(first.Outputs, output => output.Kind == "message").Content);
        Assert.False(first.IdempotentReplay);
        Assert.True(replay.IdempotentReplay);
        Assert.Equal(first.RequestId, replay.RequestId);
    }

    [Fact]
    public async Task Validate_UsesParserAndDoesNotRunCommandBody()
    {
        await using TestHarness harness = await TestHarness.CreateAsync(executionEnabled: false);

        McpCommandExecutionResult valid = await harness.Service.InvokeAsync(new(
            "randomnumber 1 3",
            harness.UserId.ToString(),
            harness.ChannelId.ToString()));
        McpCommandExecutionResult malformed = await harness.Service.InvokeAsync(new(
            "randomnumber nope 3",
            harness.UserId.ToString(),
            harness.ChannelId.ToString()));

        Assert.True(valid.Success, $"{valid.Error}: {valid.ErrorReason}");
        Assert.Equal("valid", valid.Status);
        Assert.False(valid.Executed);
        Assert.Empty(valid.Outputs);
        Assert.False(malformed.Success);
        Assert.Equal(CommandError.ParseFailed.ToString(), malformed.Error);
    }

    [Fact]
    public async Task Validate_HonorsCallerCancellation()
    {
        await using TestHarness harness = await TestHarness.CreateAsync(executionEnabled: false);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Service.InvokeAsync(
            new McpCommandInvocation(
                "echo cancelled",
                harness.UserId.ToString(),
                harness.ChannelId.ToString()),
            cancellation.Token));
    }

    [Fact]
    public async Task Execute_DoesNotStartWhenAlreadyCancelled()
    {
        await using TestHarness harness = await TestHarness.CreateAsync(executionEnabled: true);
        McpCommandInvocation invocation = new(
            "echo runs once",
            harness.UserId.ToString(),
            harness.ChannelId.ToString(),
            SourceMessageId: "900000000000005",
            Mode: "execute",
            IdempotencyKey: "test-cancelled-key-0001");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.Service.InvokeAsync(invocation, cancellation.Token));
        McpCommandExecutionResult executed = await harness.Service.InvokeAsync(invocation);

        Assert.True(executed.Success, $"{executed.Error}: {executed.ErrorReason}");
        Assert.Equal("runs once", Assert.Single(executed.Outputs, output => output.Kind == "message").Content);
    }

    [Fact]
    public async Task Invocation_ValidatesLocaleAndTimeZoneContext()
    {
        await using TestHarness harness = await TestHarness.CreateAsync(executionEnabled: false);

        McpCommandExecutionResult validTimeZone = await harness.Service.InvokeAsync(new(
            "echo localized",
            harness.UserId.ToString(),
            harness.ChannelId.ToString(),
            TimeZoneId: TimeZoneInfo.Utc.Id));

        Assert.True(validTimeZone.Success, $"{validTimeZone.Error}: {validTimeZone.ErrorReason}");

        if (CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Any(culture => culture.Name.Equals("en-US", StringComparison.OrdinalIgnoreCase)))
        {
            McpCommandExecutionResult validLocale = await harness.Service.InvokeAsync(new(
                "echo localized",
                harness.UserId.ToString(),
                harness.ChannelId.ToString(),
                Locale: "en-US"));

            Assert.True(validLocale.Success, $"{validLocale.Error}: {validLocale.ErrorReason}");
        }

        await Assert.ThrowsAsync<ArgumentException>(() => harness.Service.InvokeAsync(new(
            "echo localized",
            harness.UserId.ToString(),
            harness.ChannelId.ToString(),
            Locale: "definitely-not-a-real-locale")));
        await Assert.ThrowsAsync<ArgumentException>(() => harness.Service.InvokeAsync(new(
            "echo localized",
            harness.UserId.ToString(),
            harness.ChannelId.ToString(),
            TimeZoneId: "Not/A_Real_Time_Zone")));
    }

    [Fact]
    public async Task Validate_PreservesGuildPreconditionsAndHidesOwnerCommands()
    {
        await using TestHarness harness = await TestHarness.CreateAsync(executionEnabled: false);

        McpCommandExecutionResult missingGuild = await harness.Service.InvokeAsync(new(
            "toggleactivityroles",
            harness.UserId.ToString(),
            harness.ChannelId.ToString()));
        McpCommandExecutionResult hidden = await harness.Service.InvokeAsync(new(
            "dumplogs",
            harness.UserId.ToString(),
            harness.ChannelId.ToString()));

        Assert.False(missingGuild.Success);
        Assert.Equal(CommandError.UnmetPrecondition.ToString(), missingGuild.Error);
        Assert.False(hidden.Success);
        Assert.Equal(CommandError.UnknownCommand.ToString(), hidden.Error);
    }

    [Fact]
    public async Task Execute_IsDisabledByDefaultAndRequiresIdempotencyKey()
    {
        await using TestHarness harness = await TestHarness.CreateAsync(executionEnabled: false);
        McpCommandInvocation withoutKey = new(
            "echo test",
            harness.UserId.ToString(),
            harness.ChannelId.ToString(),
            SourceMessageId: "900000000000005",
            Mode: "execute");

        await Assert.ThrowsAsync<ArgumentException>(() => harness.Service.InvokeAsync(withoutKey));

        McpCommandInvocation disabled = withoutKey with { IdempotencyKey = "test-disabled-key-0001" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Service.InvokeAsync(disabled));
    }

    [Fact]
    public async Task AttachmentValidation_AllowsOnlyBoundedDiscordCdnInputs()
    {
        await using TestHarness harness = await TestHarness.CreateAsync(executionEnabled: false);

        harness.Service.ValidateAttachments([
            new McpCommandAttachment("image.png", "https://cdn.discordapp.com/attachments/1/2/image.png", 1024, "image/png")
        ]);
        Assert.Throws<ArgumentException>(() => harness.Service.ValidateAttachments([
            new McpCommandAttachment("image.png", "https://example.com/image.png", 1024, "image/png")
        ]));
        Assert.Throws<ArgumentException>(() => harness.Service.ValidateAttachments([
            new McpCommandAttachment("..\\secret.txt", "https://cdn.discordapp.com/attachments/1/2/file.txt", 10)
        ]));
        Assert.Throws<ArgumentException>(() => harness.Service.ValidateAttachments([
            new McpCommandAttachment("image.png", $"https://cdn.discordapp.com/{new string('a', 2048)}", 10)
        ]));
    }

    [Fact]
    public async Task Execute_RequiresVerifiedSourceMessageAndRejectsInvalidResponseMode()
    {
        await using TestHarness harness = await TestHarness.CreateAsync(executionEnabled: true);
        McpCommandInvocation attachment = new(
            "deepfry",
            harness.UserId.ToString(),
            harness.ChannelId.ToString(),
            Attachments: [new("image.png", "https://cdn.discordapp.com/attachments/1/2/image.png", 1024)],
            Mode: "execute",
            IdempotencyKey: "test-attachment-key-001");
        McpCommandInvocation invalidResponse = attachment with
        {
            Attachments = null,
            ResponseMode = "somewhere"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => harness.Service.InvokeAsync(attachment));
        await Assert.ThrowsAsync<ArgumentException>(() => harness.Service.InvokeAsync(invalidResponse));
    }

    [Fact]
    public async Task Capture_BoundsFilesAndOutputCount()
    {
        McpApiOptions options = new([], "test-key", 60)
        {
            MaxCapturedOutputBytes = 4,
            MaxCapturedOutputs = 2
        };
        McpCommandResponseSink sink = new(options);
        DiscordSocketClient client = new();
        IUser user = TestHarness.CreateUser(900000000000003);
        IMessageChannel channel = TestHarness.CreateChannel(900000000000004);
        IUserMessage message = McpDiscordMessageFactory.CreateInvocation(user, channel, "echo", [], null, sink);
        SocketCommandContextExtended context = new(client, message, null, null, sink);

        await context.SendFileResponseAsync(new MemoryStream([1, 2, 3, 4, 5]), "five.bin");
        await context.SendResponseAsync("second");
        await context.SendResponseAsync("third");

        McpCapturedOutput[] outputs = [.. sink.Snapshot()];
        Assert.True(outputs[0].File!.Truncated);
        Assert.Null(outputs[0].File!.Base64Data);
        Assert.Equal("output-limit", outputs[^1].Kind);
        client.Dispose();
    }

    [Fact]
    public async Task Capture_DoesNotSplitSurrogatePairsWhenTruncatingText()
    {
        McpCommandResponseSink sink = new(new McpApiOptions([], "test-key", 60));
        DiscordSocketClient client = new();
        IUser user = TestHarness.CreateUser(900000000000003);
        IMessageChannel channel = TestHarness.CreateChannel(900000000000004);
        IUserMessage message = McpDiscordMessageFactory.CreateInvocation(user, channel, "echo", [], null, sink);
        SocketCommandContextExtended context = new(client, message, null, null, sink);
        string content = new string('x', 7999) + "😀tail";

        await context.SendResponseAsync(content);

        string captured = Assert.Single(sink.Snapshot()).Content!;
        Assert.Equal(new string('x', 7999) + "…", captured);
        Assert.DoesNotContain(captured, char.IsSurrogate);
        client.Dispose();
    }

    [Fact]
    public async Task Capture_PreservesImageRichEmbedMetadata()
    {
        McpApiOptions options = new([], "test-key", 60);
        McpCommandResponseSink sink = new(options);
        DiscordSocketClient client = new();
        IUser user = TestHarness.CreateUser(900000000000003);
        IMessageChannel channel = TestHarness.CreateChannel(900000000000004);
        IUserMessage message = McpDiscordMessageFactory.CreateInvocation(user, channel, "cat", [], null, sink);
        SocketCommandContextExtended context = new(client, message, null, null, sink);
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        Embed embed = new EmbedBuilder()
            .WithTitle("Cat")
            .WithImageUrl("https://cdn.discordapp.com/cat.png")
            .WithThumbnailUrl("https://cdn.discordapp.com/cat-thumb.png")
            .WithColor(Color.Blue)
            .WithTimestamp(timestamp)
            .Build();

        await context.SendResponseAsync(embed: embed);

        McpCapturedEmbed captured = Assert.Single(Assert.Single(sink.Snapshot()).Embeds);
        Assert.Equal(embed.Image!.Value.Url, captured.ImageUrl);
        Assert.Equal(embed.Thumbnail!.Value.Url, captured.ThumbnailUrl);
        Assert.Equal(embed.Color!.Value.RawValue, captured.Color);
        Assert.Equal(embed.Timestamp, captured.Timestamp);
        client.Dispose();
    }

    [Fact]
    public async Task DiscordResponseMode_ForwardsAndCapturesResponseEditsOnce()
    {
        McpApiOptions options = new([], "test-key", 60);
        McpCommandResponseSink sink = new(options, forwardToDiscord: true);
        DiscordSocketClient client = new();
        IUser user = TestHarness.CreateUser(900000000000003);
        int sends = 0;
        int editActions = 0;
        IUserMessage sentMessage = DiscordProxy.Create<IUserMessage>((method, args) =>
        {
            if (method.Name == "ModifyAsync" && args?.FirstOrDefault() is Action<MessageProperties> update)
            {
                MessageProperties properties = new();
                update(properties);
                editActions++;
                return Task.CompletedTask;
            }

            return DiscordProxy.DefaultValue(method.ReturnType);
        });
        IMessageChannel channel = DiscordProxy.Create<IMessageChannel>((method, _) =>
        {
            if (method.Name == "SendMessageAsync")
            {
                sends++;
                return Task.FromResult(sentMessage);
            }

            return DiscordProxy.DefaultValue(method.ReturnType);
        });
        IUserMessage invocation = McpDiscordMessageFactory.CreateInvocation(user, channel, "help", [], null, sink);
        SocketCommandContextExtended context = new(client, invocation, null, null, sink);

        IUserMessage response = await context.SendResponseAsync("initial");
        await response.ModifyAsync(properties => properties.Content = "edited");

        Assert.Equal(1, sends);
        Assert.Equal(1, editActions);
        Assert.Contains(sink.Snapshot(), output => output.Kind == "message" && output.Content == "initial");
        Assert.Contains(sink.Snapshot(), output => output.Kind == "modify-response" && output.Content == "edited");
        client.Dispose();
    }

    private sealed class TestHarness : IAsyncDisposable
    {
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        private readonly DummyServiceProvider provider;

        private TestHarness(
            DiscordSocketClient client,
            CommandService commands,
            DummyServiceProvider provider,
            McpCommandExecutionService service,
            ulong userId,
            ulong channelId)
        {
            this.client = client;
            this.commands = commands;
            this.provider = provider;
            Service = service;
            UserId = userId;
            ChannelId = channelId;
        }

        public McpCommandExecutionService Service { get; }
        public ulong UserId { get; }
        public ulong ChannelId { get; }

        public static async Task<TestHarness> CreateAsync(bool executionEnabled)
        {
            ulong userId = 900000000000001;
            ulong channelId = 900000000000002;
            DiscordSocketClient client = new();
            CommandService commands = new(new CommandServiceConfig { DefaultRunMode = RunMode.Async });
            DummyServiceProvider provider = new(commands, client);
            await commands.AddModulesAsync(typeof(McpTools).Assembly, provider);

            McpApiOptions options = new([], "test-key", 60)
            {
                CommandExecutionEnabled = executionEnabled,
                CommandTimeoutSeconds = 5
            };
            McpCommandCatalog catalog = new(commands);
            McpCommandExecutionService service = new(
                commands,
                client,
                new DummyScopeFactory(provider),
                options,
                catalog,
                NullLogger<McpCommandExecutionService>.Instance)
            {
                ContextFactoryOverride = (invocation, _, sink, validation, _) =>
                {
                    IUser user = CreateUser(userId);
                    IMessageChannel channel = CreateChannel(channelId);
                    IUserMessage message = McpDiscordMessageFactory.CreateInvocation(
                        user,
                        channel,
                        invocation.MessageContent ?? invocation.Command,
                        invocation.Attachments ?? [],
                        null,
                        sink);
                    return Task.FromResult(new SocketCommandContextExtended(
                        client,
                        message,
                        guild: null,
                        user: null,
                        sink,
                        validation,
                        invocation.Locale,
                        invocation.TimeZoneId));
                }
            };
            return new TestHarness(client, commands, provider, service, userId, channelId);
        }

        internal static IUser CreateUser(ulong id) => DiscordProxy.Create<IUser>((method, _) => method.Name switch
        {
            "get_Id" => id,
            "get_Username" or "get_GlobalName" or "get_DisplayName" => "MCP Test User",
            "get_IsBot" or "get_IsWebhook" => false,
            "get_Mention" => $"<@{id}>",
            "get_CreatedAt" => DateTimeOffset.UtcNow,
            "ToString" => "MCP Test User",
            _ => DiscordProxy.DefaultValue(method.ReturnType)
        });

        internal static IMessageChannel CreateChannel(ulong id) => DiscordProxy.Create<IMessageChannel>((method, _) => method.Name switch
        {
            "get_Id" => id,
            "get_Name" => "mcp-test",
            "get_CreatedAt" => DateTimeOffset.UtcNow,
            "ToString" => "mcp-test",
            _ => DiscordProxy.DefaultValue(method.ReturnType)
        });

        public ValueTask DisposeAsync()
        {
            Service.Dispose();
            ((IDisposable)commands).Dispose();
            client.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DummyServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> instances;

        public DummyServiceProvider(CommandService commands, DiscordSocketClient client)
        {
            instances = new Dictionary<Type, object>
            {
                [typeof(CommandService)] = commands,
                [typeof(DiscordSocketClient)] = client,
                [typeof(InteractionsHandler)] = new InteractionsHandler(client)
            };
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceProvider))
                return this;
            if (instances.TryGetValue(serviceType, out object? instance))
                return instance;
            if (!serviceType.IsClass)
                return null;

            instance = RuntimeHelpers.GetUninitializedObject(serviceType);
            instances[serviceType] = instance;
            return instance;
        }
    }

    private sealed class DummyScopeFactory(IServiceProvider provider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new DummyScope(provider);
    }

    private sealed class DummyScope(IServiceProvider provider) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = provider;
        public void Dispose() { }
    }
}
