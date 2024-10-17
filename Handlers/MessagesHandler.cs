using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Utilities;
using System.Reflection;

namespace Morpheus.Handlers;
public class MessagesHandler(DiscordSocketClient client, CommandService commands, IServiceProvider serviceProvider, DB dbContext)
{
    public async Task InstallCommandsAsync()
    {
        client.MessageReceived += HandleCommandAsync;

        await commands.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);
    }

    private async Task HandleCommandAsync(SocketMessage messageParam)
    {
        // Don't process the command if it was a system message
        var message = messageParam as SocketUserMessage;
        if (message == null) return;

        // Create a number to track where the prefix ends and the command begins
        int argPos = 0;

        Guild? guild = null;
        User? user = await dbContext.Users.FirstOrDefaultAsync(u => u.DiscordId == message.Author.Id);

        if (message.Channel is SocketGuildChannel guildChannel)
            guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == guildChannel.Guild.Id);

        // Determine if the message is a command based on the prefix and make sure no bots trigger commands
        if (!(message.HasStringPrefix(guild?.Prefix ?? Env.Variables["BOT_DEFAULT_COMMAND_PREFIX"], ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos)) || message.Author.IsBot)
            return;

        // Create a WebSocket-based command context based on the message
        var context = new SocketCommandContextExtended(client, message, guild, user);

        // Execute the command with the command context we just
        // created, along with the service provider for precondition checks.
        var result = await commands.ExecuteAsync(context, argPos, serviceProvider);

        if (result.IsSuccess) return;

        _ = result.Error switch
        {
            CommandError.UnknownCommand => await context.Channel.SendMessageAsync("Unknown command."),
            CommandError.BadArgCount => await context.Channel.SendMessageAsync("Invalid number of arguments."),
            CommandError.ParseFailed => await context.Channel.SendMessageAsync("Failed to parse arguments."),
            CommandError.ObjectNotFound => await context.Channel.SendMessageAsync("Object not found."),
            CommandError.MultipleMatches => await context.Channel.SendMessageAsync("Multiple matches found."),
            CommandError.UnmetPrecondition => await context.Channel.SendMessageAsync("Unmet precondition."),
            CommandError.Exception => await context.Channel.SendMessageAsync("An exception occurred."),
            CommandError.Unsuccessful => await context.Channel.SendMessageAsync("Unsuccessful."),
            _ => await context.Channel.SendMessageAsync("An unknown error occurred.")
        };
    }
}
