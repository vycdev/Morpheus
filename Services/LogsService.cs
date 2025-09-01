using Discord;
using Discord.Commands;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Morpheus.Services;

public class LogsService(IServiceScopeFactory scopeFactory)
{
    public void Log(string message, LogSeverity severity = LogSeverity.Info)
    {
        string log = $"{$"[General/{severity}]",-20} {message}";

        Console.WriteLine(log);
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Morpheus.Database.DB>();
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
        string log;
        if (message.Exception is CommandException cmdException)
        {
            log = $"{$"[Command/{message.Severity}]",-20} {cmdException.Command.Aliases[0]}"
                + $" failed to execute in {cmdException.Context.Channel}. \n {cmdException}";
        }
        else
            log = $"{$"[General/{message.Severity}]",-20} {message}";

        Console.WriteLine(log);
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Morpheus.Database.DB>();
        dbContext.Add(new Log()
        {
            Message = log,
            Severity = (int)message.Severity,
            Version = Utils.GetAssemblyVersion()
        });

        dbContext.SaveChanges();
    }

}
