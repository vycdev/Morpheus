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
        await dbContext.SaveChangesAsync();

        logsService.Log($"New guild created {name}", Discord.LogSeverity.Verbose);
        guildPrefixService.SetPrefix(discordId, guildDb.Prefix);

        return guildDb;
    }
}
