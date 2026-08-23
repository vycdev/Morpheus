using System.Collections.Concurrent;
using Discord.WebSocket;

namespace Morpheus.Handlers;

public class InteractionsHandler(DiscordSocketClient client)
{
    private static readonly ConcurrentDictionary<string, Func<SocketInteraction, Task>> InteractionIds = [];

    public void RegisterInteraction(string id, Func<SocketInteraction, Task> func)
    {
        if (InteractionIds.TryAdd(id, func))
            client.InteractionCreated += func;
    }
}
