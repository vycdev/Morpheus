using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;

namespace Morpheus.Services;

public class ChannelService(DB dbContext, LogsService logsService)
{
    public async Task<Channel> TryGetCreateChannel(ulong discordId, string name)
    {
        Channel? channel = await dbContext.Channels.FirstOrDefaultAsync(c => c.DiscordId == discordId);

        if (channel != null)
            return channel;

        channel = new Channel
        {
            DiscordId = discordId,
            Name = name
        };

        await dbContext.Channels.AddAsync(channel);
        await dbContext.SaveChangesAsync();

        logsService.Log($"New channel created {name}", Discord.LogSeverity.Verbose);

        return channel;
    }
}
