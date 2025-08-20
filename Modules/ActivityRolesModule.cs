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
    public async Task ToggleActivityRolesAsync()
    {
        if (Context.DbGuild == null)
        {
            await ReplyAsync("This command can only be used in a guild.");
            return;
        }

        Context.DbGuild.UseActivityRoles = !Context.DbGuild.UseActivityRoles;
        dbContext.Guilds.Update(Context.DbGuild);

        // Get activity roles for the guild and delete them if they exist 
        // For each type in ActivityRoleType
        await ReplyAsync("Attempting to delete existing activity roles if they are present...");
        foreach (RoleType roleType in Enum.GetValues(typeof(ActivityRoleType)).Cast<RoleType>())
        {
            // Find the role in the database
            Role? role = dbContext.Roles.FirstOrDefault(r => r.GuildId == Context.DbGuild.Id && r.RoleType == roleType);

            if (role == null)
            {
                continue; // If the role doesn't exist, skip to the next one
            }

            // Find the role in the guild
            SocketRole guildRole = Context.Guild.GetRole(role.RoleId);

            if (guildRole == null)
            {
                continue; // If the role doesn't exist in the guild, skip to the next one
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

            // Remove the role from the database
            dbContext.Roles.RemoveRange(dbContext.Roles.Where(r => r.GuildId == Context.DbGuild.Id && r.RoleType == roleType));
        }

        // If activity roles have been enabled
        if (Context.DbGuild.UseActivityRoles)
        {
            await ReplyAsync("Activity roles have been enabled. Creating new roles...");
            List<RestRole> newRoles = [];

            // For each type in RoleType
            foreach (RoleType roleType in Enum.GetValues(typeof(ActivityRoleType)).Cast<RoleType>())
            {
                RestRole role = await Context.Guild.CreateRoleAsync(name: roleType.GetDisplayName(), color: roleType.GetDiscordColor(), isHoisted: false);
                newRoles.Add(role);

                // Add the role to the database
                dbContext.Roles.Add(new Role
                {
                    RoleId = role.Id,
                    GuildId = Context.DbGuild.Id,
                    RoleType = roleType,
                });
            }

            await ReplyAsync("New roles have been created. Grabbing most active users...");

            // Assign the roles to the users based on their activity 
            List<UserLevels> userLevels = activityService.GetTopActivity(Context.DbGuild.Id);
            List<List<User>> slices = activityService.GetUserSlices(userLevels, [0.01, 0.05, 0.10, 0.20, 0.30]);

            await ReplyAsync($"Top 1%: {slices[0].Count}, Top 5%: {slices[1].Count}, Top 10%: {slices[2].Count}, Top 20%: {slices[3].Count}, Top 30%: {slices[4].Count}");
            await ReplyAsync("Assigning roles to users. This might take a while...");
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    foreach (User user in slices[i])
                    {
                        SocketGuildUser guildUser = Context.Guild.GetUser(user.DiscordId);

                        if (guildUser != null)
                        {
                            await guildUser.AddRoleAsync(newRoles[i].Id);
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

        string status = Context.DbGuild.UseActivityRoles ? "enabled" : "disabled";
        await ReplyAsync($"Activity roles have been {status} for this guild.");

        if (Context.DbGuild.UseActivityRoles)
        {
            await ReplyAsync("New roles have been automatically created and users have been assigned.");
            await ReplyAsync("You can customize the roles however you want. But if you delete a role a new one will be created in it's place");
            await ReplyAsync("Make sure the bot can continue to manage the created roles.");
        }
    }
}
