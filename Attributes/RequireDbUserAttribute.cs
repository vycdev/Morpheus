using Discord.Commands;
using Morpheus.Extensions;

namespace Morpheus.Attributes;

/// <summary>
/// Checks if the db user is present in the message context 
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RequireDbUserAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        if (context is not SocketCommandContextExtended contextExtended)
            return Task.FromResult(PreconditionResult.FromError("This command can only be used in a guild."));

        if (contextExtended.DbUser == null)
            return Task.FromResult(PreconditionResult.FromError("Your user hasn't been added to the database yet, please try again."));

        return Task.FromResult(PreconditionResult.FromSuccess());
    }
}
