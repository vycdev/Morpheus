using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;

namespace Morpheus.Services;

public class GuildService(DB dbContext, LogsService logsService, GuildPrefixService guildPrefixService)
{
    public Task<Guild> TryGetCreateGuild(SocketGuild guild) =>
        TryGetCreateGuild(guild.Id, guild.Name);

    internal async Task<Guild> TryGetCreateGuild(ulong discordId, string name)
    {
        Guild? guildDb = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == discordId);

        if (guildDb != null)
        {
            if (guildDb.Name != name)
            {
                guildDb.Name = name;
                await dbContext.SaveChangesAsync();
            }

            return guildDb;
        }

        guildDb = new Guild
        {
            DiscordId = discordId,
            Name = name,
            Prefix = guildPrefixService.DefaultPrefix
        };

        await dbContext.Guilds.AddAsync(guildDb);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another handler may have created the same Discord guild after our initial lookup.
            // Clear the failed insert and use the row protected by the unique DiscordId index.
            dbContext.ChangeTracker.Clear();
            Guild? concurrentGuild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == discordId);
            if (concurrentGuild == null)
                throw;

            if (concurrentGuild.Name != name)
            {
                concurrentGuild.Name = name;
                await dbContext.SaveChangesAsync();
            }

            guildPrefixService.SetPrefix(discordId, concurrentGuild.Prefix);
            return concurrentGuild;
        }

        logsService.Log($"New guild created {name}", Discord.LogSeverity.Verbose);
        guildPrefixService.SetPrefix(discordId, guildDb.Prefix);

        return guildDb;
    }
}
