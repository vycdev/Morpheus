using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Database.Models;

namespace Morpheus.Extensions;
public class SocketCommandContextExtended(
    DiscordSocketClient client, 
    SocketUserMessage msg, 
    Guild? guild, 
    User? user
) : SocketCommandContext(client, msg)
{
    public Guild? DbGuild { get; set; } = guild;
    public User? DbUser { get; set; } = user;
}
