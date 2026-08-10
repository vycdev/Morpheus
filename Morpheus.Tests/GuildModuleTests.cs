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

    [Theory]
    [InlineData("m!", "m!")]
    [InlineData("?", "?")]
    [InlineData("abc", "abc")]
    public void TryValidatePrefix_AcceptsPrintablePrefixes(string value, string expected)
    {
        Assert.True(GuildModule.TryValidatePrefix(value, out string prefix));
        Assert.Equal(expected, prefix);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcd")]
    [InlineData("m !")]
    [InlineData("m\n")]
    public void TryValidatePrefix_RejectsWhitespaceAndInvalidLengths(string? value)
    {
        Assert.False(GuildModule.TryValidatePrefix(value, out _));
    }
}
