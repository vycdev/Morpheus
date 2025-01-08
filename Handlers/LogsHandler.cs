using Discord.Commands;
using Discord.WebSocket;
using Discord;
using Morpheus.Database;
using Morpheus.Database.Models;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Utilities;

namespace Morpheus.Handlers;
internal class LogsHandler
{
    private readonly IServiceScopeFactory scopefa;
    public LogsHandler(DiscordSocketClient client, CommandService command, IServiceScopeFactory serviceScopeFactory)
    {
        scopefa = serviceScopeFactory;

        client.Log += LogAsync;
        command.Log += LogAsync;
    }

    private Task LogAsync(LogMessage message)
    {
        using IServiceScope scope = scopefa.CreateScope();
        DB dbContext = scope.ServiceProvider.GetRequiredService<DB>();

        string log = string.Empty;
        if (message.Exception is CommandException cmdException)
        {
            log = $"{$"[Command/{message.Severity}]",-20} {cmdException.Command.Aliases.First()}"
                + $" failed to execute in {cmdException.Context.Channel}. \n {cmdException}";
        }
        else
            log = $"{$"[General/{message.Severity}]",-20} {message}";

        Console.WriteLine(log);
        dbContext.Add(new Log()
        {
            Message = log,
            Severity = (int)message.Severity,
            Version = Utils.GetAssemblyVersion()
        });

        dbContext.SaveChanges();

        return Task.CompletedTask;
    }
}
