using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Services;

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
        LogsService logsService = scope.ServiceProvider.GetRequiredService<LogsService>();

        logsService.Log(message);

        return Task.CompletedTask;
    }
}
