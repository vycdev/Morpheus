using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Services;
using Morpheus.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Collections.Concurrent;

namespace Morpheus.Handlers;

public class MessagesHandler
{
    private readonly DiscordSocketClient client;
    private readonly CommandService commands;
    private readonly IServiceProvider serviceProvider;
    private readonly IServiceScopeFactory scopeFactory;
    private static bool started = false;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, IServiceScope> commandScopes = new();

    public MessagesHandler(DiscordSocketClient client, CommandService commands, IServiceProvider serviceProvider, IServiceScopeFactory scopeFactory)
    {
        if (started)
            throw new InvalidOperationException("At most one instance of this service can be started");

        started = true;

        this.client = client;
        this.commands = commands;
        this.serviceProvider = serviceProvider;
        this.scopeFactory = scopeFactory;

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

        // Create a number to track where the prefix ends and the command begins
        int argPos = 0;

        using IServiceScope scope = scopeFactory.CreateScope();
        var usersService = scope.ServiceProvider.GetRequiredService<UsersService>();
        var guildService = scope.ServiceProvider.GetRequiredService<GuildService>();

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
        var commandScope = scopeFactory.CreateScope();
        // Track scope by message id so we can dispose it when the command actually finishes (RunMode.Async)
        commandScopes[message.Id] = commandScope;
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
    }

    private Task OnCommandExecuted(Optional<CommandInfo> command, ICommandContext context, IResult result)
    {
        try
        {
            if (context?.Message != null && commandScopes.TryRemove(context.Message.Id, out var scope))
            {
                scope.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MessagesHandler] Failed to dispose command scope: {ex}");
        }
        return Task.CompletedTask;
    }
}
