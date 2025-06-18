using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;

namespace Morpheus.Services;
public class GuildService(DB dbContext, LogsService logsService)
{
    public async Task<Guild> TryGetCreateGuild(SocketGuild guild)
    {
        var guildDb = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == guild.Id);

        if (guildDb != null)
            return guildDb;

        guildDb = new Guild
        {
            DiscordId = guild.Id,
            Name = guild.Name
        };

        await dbContext.Guilds.AddAsync(guildDb);
        await dbContext.SaveChangesAsync();

        logsService.Log($"New guild created {guild.Name}", Discord.LogSeverity.Verbose);

        return guildDb;
    }
}
