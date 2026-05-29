using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Services;

namespace Morpheus.Handlers;

internal class LogsHandler
{
    private readonly LogsService logsService;

    public LogsHandler(DiscordSocketClient client, CommandService command, LogsService logsService)
    {
        this.logsService = logsService;

        client.Log += LogAsync;
        command.Log += LogAsync;
    }

    private Task LogAsync(LogMessage message)
    {
        logsService.Log(message);

        return Task.CompletedTask;
    }
}
