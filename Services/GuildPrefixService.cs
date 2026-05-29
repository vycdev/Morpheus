using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Database;
using Morpheus.Utilities;

namespace Morpheus.Services;

public class GuildPrefixService(IServiceScopeFactory scopeFactory)
{
    private readonly ConcurrentDictionary<ulong, string> prefixesByGuildId = [];
    public string DefaultPrefix { get; } = Env.Get("BOT_DEFAULT_COMMAND_PREFIX", "m!");

    public async Task<string> GetPrefixAsync(ulong guildDiscordId)
    {
        if (prefixesByGuildId.TryGetValue(guildDiscordId, out string? prefix))
            return prefix;

        using IServiceScope scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DB>();

        prefix = await dbContext.Guilds
            .AsNoTracking()
            .Where(g => g.DiscordId == guildDiscordId)
            .Select(g => g.Prefix)
            .FirstOrDefaultAsync()
            ?? DefaultPrefix;

        prefixesByGuildId[guildDiscordId] = prefix;

        return prefix;
    }

    public void SetPrefix(ulong guildDiscordId, string prefix)
    {
        prefixesByGuildId[guildDiscordId] = prefix;
    }
}
