using Discord;
using Discord.Commands;
using Morpheus.Utilities;

namespace Morpheus.Services;

public class LogsService(LogQueue logQueue)
{
    public void Log(string message, LogSeverity severity = LogSeverity.Info)
    {
        QueuedLog log = CreateGeneralLog(message, severity);

        WriteAndEnqueue(log);
    }

    public void Log(LogMessage message)
    {
        QueuedLog log = CreateDiscordLog(message);

        WriteAndEnqueue(log);
    }

    internal static QueuedLog CreateGeneralLog(string message, LogSeverity severity, DateTime? insertDate = null) =>
        new(FormatGeneralLog(message, severity), (int)severity, Utils.GetAssemblyVersion(), insertDate ?? DateTime.UtcNow);

    internal static QueuedLog CreateDiscordLog(LogMessage message, DateTime? insertDate = null) =>
        new(FormatDiscordLog(message), (int)message.Severity, Utils.GetAssemblyVersion(), insertDate ?? DateTime.UtcNow);

    internal static string FormatGeneralLog(string message, LogSeverity severity) =>
        $"{$"[General/{severity}]",-20} {message}";

    internal static string FormatDiscordLog(LogMessage message)
    {
        if (message.Exception is CommandException cmdException)
        {
            string commandName = cmdException.Command?.Aliases.FirstOrDefault() ?? "unknown";
            return $"{$"[Command/{message.Severity}]",-20} {commandName}"
                + $" failed to execute in {cmdException.Context.Channel}. \n {cmdException}";
        }

        return FormatGeneralLog(message.ToString(), message.Severity);
    }

    private void WriteAndEnqueue(QueuedLog log)
    {
        Console.WriteLine(log.Message);

        if (!logQueue.TryEnqueue(log))
            Console.WriteLine(FormatGeneralLog("Log queue is full; dropping database log entry.", LogSeverity.Warning));
    }
}
