using Discord.WebSocket;

namespace Morpheus.Handlers;
public class InteractionsHandler(DiscordSocketClient client)
{
    static readonly Dictionary<string, Func<SocketInteraction, Task>> InteractionIds = [];
    private static bool _routedRegistered;

    public void RegisterInteraction(string id, Func<SocketInteraction, Task> func)
    {
        if (!InteractionIds.TryAdd(id, func))
            return;

        if (!_routedRegistered)
        {
            client.InteractionCreated += RouteInteraction;
            _routedRegistered = true;
        }
    }

    private static async Task RouteInteraction(SocketInteraction interaction)
    {
        if (interaction is SocketMessageComponent messageComponent)
        {
            string customId = messageComponent.Data.CustomId;
            if (customId != null && InteractionIds.TryGetValue(customId, out var handler))
            {
                await handler(interaction);
            }
        }
    }
}
