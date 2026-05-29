using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Morpheus.Handlers;

public class MessagesHandler
{
    private readonly DiscordSocketClient client;
    private readonly CommandService commands;
    private readonly IServiceProvider serviceProvider;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly GuildPrefixService guildPrefixService;
    private static bool started = false;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, IServiceScope> commandScopes = new();

    public MessagesHandler(DiscordSocketClient client, CommandService commands, IServiceProvider serviceProvider, IServiceScopeFactory scopeFactory, GuildPrefixService guildPrefixService)
    {
        if (started)
            throw new InvalidOperationException("At most one instance of this service can be started");

        started = true;

        this.client = client;
        this.commands = commands;
        this.serviceProvider = serviceProvider;
        this.scopeFactory = scopeFactory;
        this.guildPrefixService = guildPrefixService;

        client.MessageReceived += HandleMessageAsync;
        this.commands.CommandExecuted += OnCommandExecuted;
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

        if (message.Author.IsBot)
            return;

        int argPos = 0;
        bool isCommand = message.HasMentionPrefix(client.CurrentUser, ref argPos);
        if (!isCommand)
        {
            string commandPrefix = guildPrefixService.DefaultPrefix;
            if (message.Channel is SocketGuildChannel guildChannel)
                commandPrefix = await guildPrefixService.GetPrefixAsync(guildChannel.Guild.Id);

            argPos = 0;
            isCommand = message.HasStringPrefix(commandPrefix, ref argPos);
        }

        if (!isCommand)
            return;

        var commandScope = scopeFactory.CreateScope();
        commandScopes[message.Id] = commandScope;

        try
        {
            var usersService = commandScope.ServiceProvider.GetRequiredService<UsersService>();
            var guildService = commandScope.ServiceProvider.GetRequiredService<GuildService>();

            Guild? guild = null;
            User user = await usersService.TryGetCreateUser(message.Author);
            await usersService.TryUpdateUsername(message.Author, user);

            if (message.Channel is SocketGuildChannel guildChannel)
                guild = await guildService.TryGetCreateGuild(guildChannel.Guild);

            SocketCommandContextExtended context = new(client, message, guild, user);

            IResult result = await commands.ExecuteAsync(context, argPos, commandScope.ServiceProvider);

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

            DisposeCommandScope(message.Id);
        }
        catch
        {
            DisposeCommandScope(message.Id);
            throw;
        }
    }

    private Task OnCommandExecuted(Optional<CommandInfo> command, ICommandContext context, IResult result)
    {
        try
        {
            if (context?.Message != null)
                DisposeCommandScope(context.Message.Id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MessagesHandler] Failed to dispose command scope: {ex}");
        }
        return Task.CompletedTask;
    }

    private void DisposeCommandScope(ulong messageId)
    {
        if (commandScopes.TryRemove(messageId, out var scope))
            scope.Dispose();
    }
}
