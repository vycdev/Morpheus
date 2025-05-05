using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;

namespace Morpheus.Services;
public class UsersService(DB dbContext, LogsService logsService)
{
    public async Task<User?> TryGetCreateUser(SocketUser user)
    {
        User? userDb = await dbContext.Users.FirstOrDefaultAsync(u => u.DiscordId == user.Id);

        if (userDb != null)
            return userDb;

        userDb = new User()
        {
            DiscordId = user.Id,
            Username = user.Username,
            InsertDate = DateTime.UtcNow,
            LastUsernameCheck = DateTime.UtcNow
        };

        await dbContext.Users.AddAsync(userDb);
        await dbContext.SaveChangesAsync();

        logsService.Log($"New user created {user.Username}", Discord.LogSeverity.Verbose);

        return userDb; 
    }

    public async Task TryUpdateUsername(SocketUser socketUser, User user)
    {
        if(user == null) 
            return;

        if(DateTime.UtcNow < user.LastUsernameCheck.AddDays(10))
            return;

        user.Username = socketUser.Username;
        user.LastUsernameCheck = DateTime.UtcNow;

        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync();

        logsService.Log($"New user username updated {user.Username}", Discord.LogSeverity.Verbose);

        return; 
    }
}
