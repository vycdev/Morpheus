using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
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

    private void LogWarning(string message, Exception ex) =>
        logsService.Log($"Quartz Job - {message}: {ex.GetType().Name}: {ex.Message}", LogSeverity.Warning);

    private void LogError(string message, Exception ex) =>
        logsService.Log($"Quartz Job - {message}: {ex.GetType().Name}: {ex.Message}", LogSeverity.Error);

    public async Task Execute(IJobExecutionContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;

        // Take all guilds that has activity roles enabled
        List<Guild> guilds = await dB.Guilds
            .Where(g => g.UseActivityRoles)
            .ToListAsync(cancellationToken);
        if (!guilds.Any())
        {
            Log("No guilds with activity roles enabled found.");
            return;
        }

        foreach (var guild in guilds)
        {
            try
            {
                // Find discord guild by ID
                SocketGuild? discordGuild = discordSocketClient.Guilds.FirstOrDefault(g => g.Id == guild.DiscordId);

                if (discordGuild == null)
                {
                    Log($"Guild with ID {guild.DiscordId} not found in Discord.");
                    continue; // Skip to the next guild if not found
                }

                Log($"Grabbing guild users for {guild.Name}.");
                cancellationToken.ThrowIfCancellationRequested();
                List<IGuildUser> guildUsers = [.. (await discordGuild.GetUsersAsync().FlattenAsync()).Where(u => !u.IsBot)];

                Log($"Grabbing most active users for {guild.Name}.");
                cancellationToken.ThrowIfCancellationRequested();
                ActivityRoleAssignmentResult assignments = activityService.GetActivityRoleAssignments(guild.Id, guildUsers.Select(u => u.Id));

                Log($"Eligible active users: {assignments.EligibleUserCount}");
                Log(FormatAssignmentCounts(assignments));
                Log("Reconciling activity roles. This might take a while...");

                foreach (ActivityRoleDefinition definition in ActivityService.ActivityRoleDefinitions)
                {
                    RoleType roleType = definition.RoleType;
                    try
                    {
                        Role? role = await EnsureActivityRole(discordGuild, guild, roleType, cancellationToken);

                        if (role == null)
                        {
                            Log($"Failed to ensure role {roleType.GetDisplayName()} in guild {guild.Name}. Skipping assignment.");
                            continue;
                        }

                        await ReconcileActivityRoleUsers(
                            discordGuild,
                            guildUsers,
                            roleType,
                            role.RoleId,
                            assignments.UsersByRole[roleType],
                            cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error reconciling role {roleType.GetDisplayName()} for guild {guild.Name}", ex);
                        dB.ChangeTracker.Clear();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogError($"Error processing activity roles for guild {guild.Name}", ex);
            }

            await Task.Delay(1000, cancellationToken);
        }
    }

    private async Task<Role?> EnsureActivityRole(SocketGuild discordGuild, Guild guild, RoleType roleType, CancellationToken cancellationToken)
    {
        List<Role> roleRecords = [.. dB.Roles.Where(r => r.GuildId == guild.Id && r.RoleType == roleType)];
        Role? role = roleRecords.FirstOrDefault(r => discordGuild.GetRole(r.RoleId) != null)
            ?? roleRecords.FirstOrDefault();
        bool dbChanged = false;

        foreach (Role duplicate in roleRecords.Where(r => role == null || r.Id != role.Id))
        {
            Log($"Removing duplicate database role record for {roleType.GetDisplayName()} in guild {guild.Name}: {duplicate.RoleId}");
            dB.Roles.Remove(duplicate);
            dbChanged = true;
        }

        if (role == null)
        {
            Log($"Role for {roleType} not found in database. Creating a new Discord role and saving it to the database.");
            RestRole guildRestRole = await discordGuild.CreateRoleAsync(name: roleType.GetDisplayName() ?? roleType.ToString(), color: roleType.GetDiscordColor(), isHoisted: false);

            role = new Role
            {
                GuildId = guild.Id,
                RoleType = roleType,
                RoleId = guildRestRole.Id
            };

            dB.Roles.Add(role);
            dbChanged = true;

            Log($"Created new role {roleType.GetDisplayName()} with ID {guildRestRole.Id} in guild {guild.Name}.");
        }
        else if (discordGuild.GetRole(role.RoleId) == null)
        {
            Log($"Role {roleType.GetDisplayName()} not found in guild {guild.Name}. Creating it.");

            RestRole guildRestRole = await discordGuild.CreateRoleAsync(name: roleType.GetDisplayName() ?? roleType.ToString(), color: roleType.GetDiscordColor(), isHoisted: false);

            role.RoleId = guildRestRole.Id;
            dB.Roles.Update(role);
            dbChanged = true;

            Log($"Created new role {roleType.GetDisplayName()} with ID {guildRestRole.Id} in guild {guild.Name}. Updated database role with the new role ID.");
        }

        if (dbChanged)
            await dB.SaveChangesAsync(cancellationToken);

        return role;
    }

    private async Task ReconcileActivityRoleUsers(
        SocketGuild discordGuild,
        List<IGuildUser> guildUsers,
        RoleType roleType,
        ulong roleId,
        IReadOnlyCollection<User> desiredUsers,
        CancellationToken cancellationToken)
    {
        string roleName = discordGuild.GetRole(roleId)?.Name ?? roleType.GetDisplayName() ?? roleType.ToString();
        HashSet<ulong> desiredUserIds = [.. desiredUsers.Select(u => u.DiscordId)];
        Dictionary<ulong, IGuildUser> guildUsersById = guildUsers.ToDictionary(u => u.Id);
        List<IGuildUser> usersWithRole = [.. guildUsers.Where(u => u.RoleIds.Contains(roleId))];
        HashSet<ulong> usersWithRoleIds = [.. usersWithRole.Select(u => u.Id)];

        List<IGuildUser> usersToRemove = [.. usersWithRole.Where(u => !desiredUserIds.Contains(u.Id))];
        Log($"Role {roleName}: {desiredUserIds.Count} desired, {usersWithRole.Count} current, {usersToRemove.Count} removals.");

        foreach (IGuildUser user in usersToRemove)
        {
            Log($"Removing role {roleName} from user {user.Username} ({user.Id}) in guild {discordGuild.Name}.");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await user.RemoveRoleAsync(roleId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to remove role {roleName} from user {user.Username} ({user.Id}) in guild {discordGuild.Name}", ex);
            }

            await Task.Delay(100, cancellationToken);
        }

        foreach (ulong desiredUserId in desiredUserIds)
        {
            if (!guildUsersById.TryGetValue(desiredUserId, out IGuildUser? user) || usersWithRoleIds.Contains(desiredUserId))
                continue;

            Log($"Adding role {roleName} to user {user.Username} ({user.Id}) in guild {discordGuild.Name}.");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await user.AddRoleAsync(roleId);
                usersWithRoleIds.Add(desiredUserId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to add role {roleName} to user {user.Username} ({user.Id}) in guild {discordGuild.Name}", ex);
            }

            await Task.Delay(100, cancellationToken);
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
