using Discord.Commands;
using Morpheus.Modules;
using System.Reflection;

namespace Morpheus.Tests;

public class HelpCommandRegistrationTests
{
    [Fact]
    public void HelpCommand_AcceptsMultiWordCommandNames()
    {
        MethodInfo method = Assert.Single(
            typeof(HelpModule).GetMethods(),
            method => method.GetCustomAttributes<CommandAttribute>()
                .Any(attribute => attribute.Text == "help"));
        System.Reflection.ParameterInfo parameter = Assert.Single(method.GetParameters());

        Assert.NotNull(parameter.GetCustomAttribute<RemainderAttribute>());
        Assert.True(parameter.HasDefaultValue);
        Assert.Null(parameter.DefaultValue);
    }
}