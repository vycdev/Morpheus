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
        {
            if (channel.Name != name)
            {
                channel.Name = name;
                await dbContext.SaveChangesAsync();
            }

            return channel;
        }

        channel = new Channel
        {
            DiscordId = discordId,
            Name = name
        };

        await dbContext.Channels.AddAsync(channel);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another handler may have created the same Discord channel after our initial lookup.
            // Clear the failed insert and use the row protected by the unique DiscordId index.
            dbContext.ChangeTracker.Clear();
            Channel? concurrentChannel = await dbContext.Channels.FirstOrDefaultAsync(c => c.DiscordId == discordId);
            if (concurrentChannel == null)
                throw;

            if (concurrentChannel.Name != name)
            {
                concurrentChannel.Name = name;
                await dbContext.SaveChangesAsync();
            }

            return concurrentChannel;
        }

        logsService.Log($"New channel created {name}", Discord.LogSeverity.Verbose);

        return channel;
    }
}
