using Discord.Commands;
using Morpheus.Modules;
using System.Reflection;

namespace Morpheus.Tests;

public class SubscriptionCommandRegistrationTests
{
    [Theory]
    [InlineData("subscribeyoutube")]
    [InlineData("subscribetwitch")]
    [InlineData("subscriberss")]
    public void BulkSubscribeCommands_ExposeOptionalRemainder(string commandName)
    {
        MethodInfo method = Assert.Single(
            typeof(SubscriptionsModule).GetMethods(),
            method => method.GetCustomAttributes<CommandAttribute>().Any(attribute => attribute.Text == commandName));
        System.Reflection.ParameterInfo parameter = Assert.Single(method.GetParameters());

        Assert.NotNull(parameter.GetCustomAttribute<RemainderAttribute>());
        Assert.True(parameter.HasDefaultValue);
        Assert.Null(parameter.DefaultValue);
    }
}
