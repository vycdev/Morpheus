using Discord.WebSocket;
using Morpheus.Modules;
using System.Reflection;

namespace Morpheus.Tests;

public class GuildModuleTests
{
    public static TheoryData<string> ChannelConfigurationMethods =>
    [
        nameof(GuildModule.SetWelcomeChanelAsync),
        nameof(GuildModule.SetPinsChannelAsync),
        nameof(GuildModule.SetLevelUpMessagesChannelAsync),
        nameof(GuildModule.SetLevelUpQuotesChannelAsync),
        nameof(GuildModule.SetQuotesApprovalChannel),
        nameof(GuildModule.SetHoneypotChannelAsync)
    ];

    [Theory]
    [MemberData(nameof(ChannelConfigurationMethods))]
    public void ChannelConfigurationCommands_AcceptOnlyTextChannels(string methodName)
    {
        MethodInfo method = typeof(GuildModule).GetMethod(methodName)!;
        ParameterInfo channelParameter = method.GetParameters()[0];

        Assert.Equal(typeof(SocketTextChannel), channelParameter.ParameterType);
    }
}
