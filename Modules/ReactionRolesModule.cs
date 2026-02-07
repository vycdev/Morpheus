using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.Services;

namespace Morpheus.Modules;

public class ReactionRolesModule : ModuleBase<SocketCommandContextExtended>
{
    private const string CustomIdPrefix = "rr:";
    private static readonly string[] NumericEmojis = ["1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟"];
    private readonly DB dbContext;
    private readonly LogsService logsService;

    public ReactionRolesModule(DB dbContext, LogsService logsService, InteractionsHandler interactionHandler)
    {
        this.dbContext = dbContext;
        this.logsService = logsService;
        interactionHandler.RegisterInteraction("reaction_roles", HandleReactionRoleInteraction);
    }

    [Name("Reaction Roles")]
    [Summary("Creates a reaction role message with either buttons or numeric reactions. Example usage: `!reactroles --buttons @Role1 @Role2` or `!reactroles --emojis @Role1 @Role2`. If no mode is specified, it defaults to buttons.")]
    [Command("reactroles")]
    [Alias("reactionroles", "rr")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(2, 30)]
    public async Task ReactionRolesAsync([Remainder] string remainder = "")
    {
        var dbGuild = Context.DbGuild;
        if (dbGuild == null)
        {
            await ReplyAsync("Guild configuration not found.");
            return;
        }

        bool useButtons = remainder.Contains("--buttons", StringComparison.OrdinalIgnoreCase);
        bool useEmojis = remainder.Contains("--emojis", StringComparison.OrdinalIgnoreCase);
        if (useButtons && useEmojis)
        {
            await ReplyAsync("Please choose only one mode: --buttons or --emojis.");
            return;
        }

        if (!useEmojis)
            useButtons = true;

        var roles = Context.Message.MentionedRoles
            .DistinctBy(r => r.Id)
            .ToList();

        if (roles.Count == 0)
        {
            await ReplyAsync("Please mention one or more roles.");
            return;
        }

        if (roles.Count > 10)
        {
            await ReplyAsync("Please provide at most 10 roles per message.");
            return;
        }

        var botUser = Context.Guild.CurrentUser;
        if (botUser == null)
        {
            await ReplyAsync("Bot user not found for this guild.");
            return;
        }

        var invalidRoles = roles
            .Where(r => r.IsEveryone || r.IsManaged || r.Position >= botUser.Hierarchy)
            .ToList();

        if (invalidRoles.Count > 0)
        {
            string names = string.Join(", ", invalidRoles.Select(r => r.Name));
            await ReplyAsync($"I cannot manage these roles due to hierarchy or role type: {names}");
            return;
        }

        if (useEmojis && !Context.Guild.CurrentUser.GuildPermissions.AddReactions)
        {
            await ReplyAsync("I need Add Reactions permission to create emoji reaction roles. Use --buttons or grant the permission.");
            return;
        }

        string content;
        ComponentBuilder? componentBuilder = null;

        if (useButtons)
        {
            string lines = string.Join("\n", roles.Select(role => $"- {role.Mention}"));
            content = $"Click a button to toggle roles:\n{lines}";

            componentBuilder = new ComponentBuilder();
            for (int i = 0; i < roles.Count; i++)
            {
                int row = i / 5;
                componentBuilder.WithButton(roles[i].Name, customId: $"{CustomIdPrefix}{roles[i].Id}", style: ButtonStyle.Secondary, row: row);
            }
        }
        else
        {
            string lines = string.Join("\n", roles.Select((role, index) => $"{NumericEmojis[index]} {role.Mention}"));
            content = $"React to toggle roles:\n{lines}";
        }

        var message = await Context.Channel.SendMessageAsync(content, components: componentBuilder?.Build(), allowedMentions: AllowedMentions.None);

        if (useEmojis)
        {
            for (int i = 0; i < roles.Count; i++)
            {
                try
                {
                    await message.AddReactionAsync(new Emoji(NumericEmojis[i]));
                }
                catch (Exception ex)
                {
                    logsService.Log($"[ReactionRoles] Failed to add reaction {NumericEmojis[i]} for message {message.Id}: {ex}", LogSeverity.Warning);
                }
            }
        }

        var reactionMessage = new ReactionRoleMessage
        {
            GuildId = dbGuild.Id,
            ChannelId = message.Channel.Id,
            MessageId = message.Id,
            UseButtons = useButtons
        };

        dbContext.ReactionRoleMessages.Add(reactionMessage);
        await dbContext.SaveChangesAsync();

        var items = roles.Select((role, index) => new ReactionRoleItem
        {
            ReactionRoleMessageId = reactionMessage.Id,
            RoleId = role.Id,
            Emoji = NumericEmojis[index],
            CustomId = $"{CustomIdPrefix}{role.Id}"
        });

        dbContext.ReactionRoleItems.AddRange(items);
        await dbContext.SaveChangesAsync();

        await ReplyAsync(useButtons ? "Button role message created." : "Reaction role message created.");
    }

    private async Task HandleReactionRoleInteraction(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent comp)
            return;

        string custom = comp.Data.CustomId ?? string.Empty;
        if (!custom.StartsWith(CustomIdPrefix))
            return;

        if (!ulong.TryParse(custom.Substring(CustomIdPrefix.Length), out ulong roleId))
        {
            await SafeRespond(comp, "Invalid role identifier.");
            return;
        }

        var messageId = comp.Message.Id;

        var item = await dbContext.ReactionRoleItems
            .Include(i => i.ReactionRoleMessage)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ReactionRoleMessage.MessageId == messageId && i.RoleId == roleId);

        if (item == null)
        {
            await SafeRespond(comp, "This reaction role message is no longer active.");
            return;
        }

        if (!item.ReactionRoleMessage.UseButtons)
        {
            await SafeRespond(comp, "This message uses emoji reactions, not buttons.");
            return;
        }

        if (comp.Channel is not SocketGuildChannel guildChannel)
        {
            await SafeRespond(comp, "This interaction only works in guild channels.");
            return;
        }

        var guild = guildChannel.Guild;
        var guildUser = guild.GetUser(comp.User.Id);
        if (guildUser == null)
        {
            await SafeRespond(comp, "User not found in this guild.");
            return;
        }

        var role = guild.GetRole(roleId);
        if (role == null || role.IsEveryone || role.IsManaged)
        {
            await SafeRespond(comp, "That role is no longer available.");
            return;
        }

        var botUser = guild.CurrentUser;
        if (botUser == null || !botUser.GuildPermissions.ManageRoles)
        {
            await SafeRespond(comp, "I do not have permission to manage roles.");
            return;
        }

        if (role.Position >= botUser.Hierarchy)
        {
            await SafeRespond(comp, "I cannot manage that role due to role hierarchy.");
            return;
        }

        try
        {
            bool hasRole = guildUser.Roles.Any(r => r.Id == role.Id);
            if (hasRole)
            {
                await guildUser.RemoveRoleAsync(role.Id);
                await SafeRespond(comp, $"Removed role: {role.Name}");
            }
            else
            {
                await guildUser.AddRoleAsync(role.Id);
                await SafeRespond(comp, $"Added role: {role.Name}");
            }
        }
        catch (Exception ex)
        {
            logsService.Log($"[ReactionRoles] Failed to toggle role {roleId} for user {comp.User.Id}: {ex}", LogSeverity.Warning);
            await SafeRespond(comp, "Failed to update your role. Please try again later.");
        }
    }

    private static async Task SafeRespond(SocketMessageComponent comp, string text)
    {
        try
        {
            await comp.RespondAsync(text, ephemeral: true);
        }
        catch (InvalidOperationException)
        {
            await comp.FollowupAsync(text, ephemeral: true);
        }
    }
}
