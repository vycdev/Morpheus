using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Morpheus.Database;
using Morpheus.Database.Enums;
using Morpheus.Database.Models;
using Morpheus.Services;
using Morpheus.Utilities.Extensions;
using Quartz;

namespace Morpheus.Jobs;

public class ActivityRolesJob(LogsService logsService, DB dB, DiscordSocketClient discordSocketClient, ActivityService activityService) : IJob
{
    private void Log(string message) =>
        logsService.Log($"Quartz Job - {message}");

    public async Task Execute(IJobExecutionContext context)
    {
        // Take all guilds that has activity roles enabled
        List<Guild> guilds = dB.Guilds.Where(g => g.UseActivityRoles).ToList();
        if (!guilds.Any())
        {
            Log("No guilds with activity roles enabled found.");
            return;
        }

        foreach (var guild in guilds)
        {
            try
            {
                // Find dcord guild by ID
                SocketGuild? discordGuild = discordSocketClient.Guilds.FirstOrDefault(g => g.Id == guild.DiscordId);

                if (discordGuild == null)
                {
                    Log($"Guild with ID {guild.DiscordId} not found in Discord.");
                    continue; // Skip to the next guild if not found
                }
                
                Log($"Grabbing most active users for {guild.Name}.");

                // Assign the roles to the users based on their activity 
                List<UserLevels> userLevels = activityService.GetTopActivity(guild.Id);
                List<List<User>> slices = activityService.GetUserSlices(userLevels, [0.01, 0.05, 0.10, 0.20, 0.30]);

                Log($"Top 1%: {slices[0].Count}, Top 5%: {slices[1].Count}, Top 10%: {slices[2].Count}, Top 20%: {slices[3].Count}, Top 30%: {slices[4].Count}");
                Log("Assigning roles to users. This might take a while...");

                foreach (RoleType roleType in Enum.GetValues(typeof(ActivityRoleType)).Cast<RoleType>())
                {
                    // Get the role from the database 
                    Role? role = dB.Roles.FirstOrDefault(r => r.GuildId == guild.Id && r.RoleType == roleType);

                    if (role == null)
                    {
                        Log($"Role for {roleType} not found in database. Proceed with creating a new discord role and save it to the database.");
                        RestRole guildRestRole = await discordGuild.CreateRoleAsync(name: roleType.GetDisplayName(), color: roleType.GetDiscordColor(), isHoisted: false);

                        // Add the role to the database if it doesn't exist
                        role = new Role
                        {
                            GuildId = guild.Id,
                            RoleType = roleType,
                            RoleId = guildRestRole.Id
                        };
                        
                        dB.Roles.Add(role);

                        Log($"Created new role {roleType.GetDisplayName()} with ID {guildRestRole.Id} in guild {guild.Name}.");
                    }

                    SocketRole guildRole = discordGuild.GetRole(role.RoleId);

                    if (guildRole == null)
                    {
                        Log($"Role {roleType.GetDisplayName()} not found in guild {guild.Name}. Creating it.");

                        RestRole guildRestRole = await discordGuild.CreateRoleAsync(name: roleType.GetDisplayName(), color: roleType.GetDiscordColor(), isHoisted: false);

                        role.RoleId = guildRestRole.Id; // Update the role ID in the database
                        dB.Roles.Update(role);

                        Log($"Created new role {roleType.GetDisplayName()} with ID {guildRestRole.Id} in guild {guild.Name}. And updated database role with new role.");

                        guildRole = discordGuild.GetRole(role.RoleId);

                        if(guildRole == null)
                        {
                            Log($"Failed to create role {roleType.GetDisplayName()} in guild {guild.Name}. Skipping assignment.");

                            continue; // Skip to the next role if creation failed
                        }
                    }

                    // Get all users for the current role type
                    Log($"Retrieving all users that currently have the role {guildRole.Name}");
                    List<IGuildUser> usersWithRole = [..(await discordGuild.GetUsersAsync().FlattenAsync()).Where(u => u.RoleIds.Contains(guildRole.Id))];
                    
                    Log($"Found {usersWithRole.Count} users with role {guildRole.Name} in guild {guild.Name}.");
                    Log($"Removing role {guildRole.Name} from users in guild {guild.Name}.");
                    foreach (var item in usersWithRole)
                    {
                        Log($"Removing role {guildRole.Name} from user {item.Username} ({item.Id}) in guild {guild.Name}.");

                        await item.RemoveRoleAsync(guildRole.Id);
                        await Task.Delay(100);
                    }

                    Log($"Removed role {guildRole.Name} from all users in guild {guild.Name}.");
                    Log($"Assigning role {guildRole.Name} to users in guild {guild.Name}.");
                    foreach (User user in slices[(int)roleType - 1])
                    {
                        SocketGuildUser guildUser = discordGuild.GetUser(user.DiscordId);

                        if (guildUser != null)
                        {
                            await guildUser.AddRoleAsync(guildRole.Id);
                            await Task.Delay(100);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error processing activity roles for guild {guild.Name}: {ex.Message}");
            }

            await Task.Delay(1000);
        }
    }
}
