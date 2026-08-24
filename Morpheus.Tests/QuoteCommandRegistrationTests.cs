using Discord;
using Discord.Commands;
using Morpheus.Modules;
using System.Reflection;

namespace Morpheus.Tests;

public class QuoteCommandRegistrationTests
{
    [Theory]
    [InlineData("addquote")]
    [InlineData("removequote")]
    public void ApprovalCommands_DoNotRequireObsoleteReactionPermission(string commandName)
    {
        MethodInfo method = Assert.Single(
            typeof(QuotesModule).GetMethods(),
            method => method.GetCustomAttributes<CommandAttribute>()
                .Any(attribute => attribute.Text == commandName));

        Assert.DoesNotContain(
            method.GetCustomAttributes<RequireBotPermissionAttribute>(),
            attribute => attribute.GuildPermission == GuildPermission.AddReactions);
    }
}
