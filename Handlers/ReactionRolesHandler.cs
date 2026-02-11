using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Database;
using Morpheus.Services;

namespace Morpheus.Handlers;

public class ReactionRolesHandler
{
    private readonly DiscordSocketClient client;
    private readonly IServiceScopeFactory scopeFactory;
    private static bool started = false;

    public ReactionRolesHandler(DiscordSocketClient client, IServiceScopeFactory scopeFactory)
    {
        if (started)
            throw new InvalidOperationException("At most one instance of this service can be started");

        started = true;

        this.client = client;
        this.scopeFactory = scopeFactory;

        client.ReactionAdded += HandleReactionAdded;
        client.ReactionRemoved += HandleReactionRemoved;
    }

    private Task HandleReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        return HandleReactionChange(message, channel, reaction, addRole: true);
    }

    private Task HandleReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        return HandleReactionChange(message, channel, reaction, addRole: false);
    }

    private async Task HandleReactionChange(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction, bool addRole)
    {
        if (reaction.UserId == client.CurrentUser.Id)
            return;

        if (reaction.User.IsSpecified && reaction.User.Value.IsBot)
            return;

        if (channel.Value is not SocketGuildChannel guildChannel)
            return;

        string? emoji = reaction.Emote?.ToString();
        if (string.IsNullOrWhiteSpace(emoji))
            return;
        ulong messageId = message.Id;

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DB>();

        var item = await dbContext.ReactionRoleItems
            .Include(i => i.ReactionRoleMessage)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ReactionRoleMessage.MessageId == messageId && i.Emoji == emoji);

        if (item == null)
            return;

        if (item.ReactionRoleMessage.UseButtons)
            return;

        var guild = guildChannel.Guild;
        var guildUser = guild.GetUser(reaction.UserId);
        if (guildUser == null)
            return;

        var role = guild.GetRole(item.RoleId);
        if (role == null || role.IsEveryone || role.IsManaged)
            return;

        var botUser = guild.CurrentUser;
        if (botUser == null || !botUser.GuildPermissions.ManageRoles)
            return;

        if (role.Position >= botUser.Hierarchy)
            return;

        try
        {
            if (addRole)
            {
                if (!guildUser.Roles.Any(r => r.Id == role.Id))
                    await guildUser.AddRoleAsync(role.Id);
            }
            else
            {
                if (guildUser.Roles.Any(r => r.Id == role.Id))
                    await guildUser.RemoveRoleAsync(role.Id);
            }
        }
        catch (Exception ex)
        {
            using var logScope = scopeFactory.CreateScope();
            var scopedLogs = logScope.ServiceProvider.GetRequiredService<LogsService>();
            scopedLogs.Log($"[ReactionRoles] Failed to {(addRole ? "add" : "remove")} role {role.Id} for user {guildUser.Id}: {ex}", LogSeverity.Warning);
        }
    }
}
