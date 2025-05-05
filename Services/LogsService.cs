using Discord;
using Discord.Commands;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Utilities;

namespace Morpheus.Services;

public class LogsService(DB dbContext)
{
    public void Log(string message, LogSeverity severity = LogSeverity.Info)
    {
        string log = $"{$"[General/{severity}]",-20} {message}";

        Console.WriteLine(log);
        dbContext.Add(new Log()
        {
            Message = log,
            Severity = (int)severity,
            Version = Utils.GetAssemblyVersion()
        });

        dbContext.SaveChanges();
    }

    public void Log(LogMessage message)
    {
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
    }

}
