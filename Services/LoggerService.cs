using Discord.Commands;
using Discord.WebSocket;
using Discord;

namespace Morpheus.Services;
internal class LoggerService
{
    public LoggerService(DiscordSocketClient client, CommandService command)
    {
        client.Log += LogAsync;
        command.Log += LogAsync;
    }

    private Task LogAsync(LogMessage message)
    {
        if (message.Exception is CommandException cmdException)
        {
            Console.WriteLine($"{$"[Command/{message.Severity}]",-20} {cmdException.Command.Aliases.First()}"
                + $" failed to execute in {cmdException.Context.Channel}.");
            Console.WriteLine(cmdException);
        }
        else
            Console.WriteLine($"{$"[General/{message.Severity}]",-20} {message}");

        return Task.CompletedTask;
    }
}
