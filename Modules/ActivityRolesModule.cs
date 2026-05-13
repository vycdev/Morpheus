using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Services;
using Morpheus.Utilities.Extensions;

namespace Morpheus.Modules;

public class ActivityRolesModule(DB dbContext, ActivityService activityService) : ModuleBase<SocketCommandContextExtended>
{
    [Name("Toggle Activity Roles")]
    [Command("toggleactivityroles")]
    [Summary("Toggles activity roles for the current guild. Activity roles are automatically managed roles based on the most active users in the guild in the past month.")]
    [Alias("toggleactivityrole", "activityroles", "activityrole")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [RateLimit(3, 60)]
    [RequireContext(ContextType.Guild)]
    public async Task ToggleActivityRolesAsync()
    {
        var dbGuild = Context.DbGuild!;

        dbGuild.UseActivityRoles = !dbGuild.UseActivityRoles;
        dbContext.Guilds.Update(dbGuild);

        // Get activity roles for the guild and delete them if they exist 
        await ReplyAsync("Attempting to delete existing activity roles if they are present...");
        List<RoleType> activityRoleTypes = [.. ActivityService.ActivityRoleDefinitions.Select(d => d.RoleType)];
        List<Role> existingRoleRecords = [.. dbContext.Roles.Where(r => r.GuildId == dbGuild.Id && activityRoleTypes.Contains(r.RoleType))];

        foreach (ulong roleId in existingRoleRecords.Select(r => r.RoleId).Distinct())
        {
            // Find the role in the guild
            SocketRole guildRole = Context.Guild.GetRole(roleId);

            if (guildRole == null)
            {
                continue; // If the role doesn't exist in the guild, only the database record needs cleanup
            }

            // Delete the role from the guild
            try
            {
                await guildRole.DeleteAsync();
                await ReplyAsync($"Deleted role: {guildRole.Name}");
            }
            catch (Exception)
            {
                await ReplyAsync($"Failed to delete role: {guildRole.Name}.");
            }
        }

        // Remove all old activity role records, including stale records whose Discord role no longer exists
        dbContext.Roles.RemoveRange(existingRoleRecords);

        // If activity roles have been enabled
        if (dbGuild.UseActivityRoles)
        {
            await ReplyAsync("Activity roles have been enabled. Creating new roles...");
            Dictionary<RoleType, RestRole> newRoles = [];

            // For each type in RoleType
            foreach (ActivityRoleDefinition definition in ActivityService.ActivityRoleDefinitions)
            {
                RoleType roleType = definition.RoleType;
                RestRole role = await Context.Guild.CreateRoleAsync(name: roleType.GetDisplayName(), color: roleType.GetDiscordColor(), isHoisted: false);
                newRoles[roleType] = role;

                // Add the role to the database
                dbContext.Roles.Add(new Role
                {
                    RoleId = role.Id,
                    GuildId = dbGuild.Id,
                    RoleType = roleType,
                });
            }

            await ReplyAsync("New roles have been created. Grabbing most active users...");

            // Assign the roles to the users based on their activity 
            List<IGuildUser> guildUsers = [.. (await Context.Guild.GetUsersAsync().FlattenAsync()).Where(u => !u.IsBot)];
            ActivityRoleAssignmentResult assignments = activityService.GetActivityRoleAssignments(dbGuild.Id, guildUsers.Select(u => u.Id));

            await ReplyAsync($"Eligible active users: {assignments.EligibleUserCount}");
            await ReplyAsync(FormatAssignmentCounts(assignments));
            await ReplyAsync("Assigning roles to users. This might take a while...");
            try
            {
                foreach (ActivityRoleDefinition definition in ActivityService.ActivityRoleDefinitions)
                {
                    RoleType roleType = definition.RoleType;
                    RestRole role = newRoles[roleType];

                    foreach (User user in assignments.UsersByRole[roleType])
                    {
                        SocketGuildUser guildUser = Context.Guild.GetUser(user.DiscordId);

                        if (guildUser != null)
                        {
                            await guildUser.AddRoleAsync(role.Id);
                            await Task.Delay(100); // Adding a small delay to avoid hitting rate limits
                        }
                    }
                }
            }
            catch (Exception)
            {
                await ReplyAsync($"Error assigning roles.");
            }
        }

        await dbContext.SaveChangesAsync();

        string status = dbGuild.UseActivityRoles ? "enabled" : "disabled";
        await ReplyAsync($"Activity roles have been {status} for this guild.");

        if (dbGuild.UseActivityRoles)
        {
            await ReplyAsync("New roles have been automatically created and users have been assigned.");
            await ReplyAsync("You can customize the roles however you want. But if you delete a role a new one will be created in it's place");
            await ReplyAsync("Make sure the bot can continue to manage the created roles.");
        }
    }

    private static string FormatAssignmentCounts(ActivityRoleAssignmentResult assignments)
    {
        return string.Join(", ", ActivityService.ActivityRoleDefinitions.Select(definition =>
        {
            assignments.UsersByRole.TryGetValue(definition.RoleType, out List<User>? users);
            return $"{definition.RoleType.GetDisplayName()}: {users?.Count ?? 0}";
        }));
    }
}
