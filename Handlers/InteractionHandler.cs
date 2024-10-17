using Discord.Commands;
using Discord.WebSocket;
using System;

namespace Morpheus.Handlers;
public class InteractionHandler(DiscordSocketClient client)
{
    static readonly Dictionary<string, Func<SocketInteraction, Task>> InteractionIds = []; 

    public void RegisterInteraction(string id, Func<SocketInteraction, Task> func)
    {
        if (InteractionIds.TryAdd(id, func))
            client.InteractionCreated += func;
    }
}
