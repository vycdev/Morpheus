using Discord.Commands;
using Discord.WebSocket;
using Discord;
using Morpheus.Database;
using System.Transactions;
using Morpheus.Database.Models;

namespace Morpheus.Handlers;
internal class LogsHandler
{
    DB dbContext;
    static bool started = false; 

    public LogsHandler(DiscordSocketClient client, CommandService command, DB dbContext)
    {
        if(started)
            throw new InvalidOperationException("At most one instance of this service can be started.");

        started = true;
        this.dbContext = dbContext;

        client.Log += LogAsync;
        command.Log += LogAsync;
    }

    private Task LogAsync(LogMessage message)
    {
        string log = string.Empty;
        if (message.Exception is CommandException cmdException)
        {
            log = ($"{$"[Command/{message.Severity}]",-20} {cmdException.Command.Aliases.First()}"
                + $" failed to execute in {cmdException.Context.Channel}. \n {cmdException}");
        }
        else
            log = ($"{$"[General/{message.Severity}]",-20} {message}");

        Console.WriteLine(log);
        dbContext.Add(new Log() { 
            Message = log, 
            Severity = (int)message.Severity
        });

        dbContext.SaveChanges();

        return Task.CompletedTask;
    }
}
