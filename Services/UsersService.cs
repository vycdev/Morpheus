using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;

namespace Morpheus.Services;
public class UsersService(DB dbContext, LogsService logsService)
{
    public Task<User> TryGetCreateUser(SocketUser user) =>
        TryGetCreateUserAsync(user.Id, user.Username);

    internal async Task<User> TryGetCreateUserAsync(ulong discordId, string username)
    {
        User? userDb = await dbContext.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);

        if (userDb != null)
            return userDb;

        userDb = new User()
        {
            DiscordId = discordId,
            Username = username,
            InsertDate = DateTime.UtcNow,
            LastUsernameCheck = DateTime.UtcNow
        };

        await dbContext.Users.AddAsync(userDb);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another message handler may have created the same Discord user after our initial
            // lookup. Re-query instead of failing activity processing on the unique index.
            dbContext.ChangeTracker.Clear();
            User? concurrentUser = await dbContext.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
            if (concurrentUser != null)
                return concurrentUser;

            throw;
        }

        logsService.Log($"New user created {username}", Discord.LogSeverity.Verbose);

        return userDb;
    }

    public async Task TryUpdateUsername(SocketUser socketUser, User user)
    {
        if (user == null)
            return;

        if (DateTime.UtcNow < user.LastUsernameCheck.AddDays(10))
            return;

        user.Username = socketUser.Username;
        user.LastUsernameCheck = DateTime.UtcNow;

        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync();

        logsService.Log($"New user username updated {user.Username}", Discord.LogSeverity.Verbose);

        return;
    }
}
