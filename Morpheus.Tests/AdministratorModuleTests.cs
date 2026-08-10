using Morpheus.Modules;

namespace Morpheus.Tests;

public class AdministratorModuleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-user-id")]
    public void TryParseOwnerId_RejectsMissingOrInvalidValues(string? value)
    {
        Assert.False(AdministratorModule.TryParseOwnerId(value, out _));
    }

    [Fact]
    public void TryParseOwnerId_AcceptsValidDiscordUserId()
    {
        Assert.True(AdministratorModule.TryParseOwnerId("123456789012345678", out ulong ownerId));
        Assert.Equal(123456789012345678UL, ownerId);
    }

    [Fact]
    public void BuildLogMessages_SplitsOversizedLinesWithinDiscordLimit()
    {
        string line = new('x', 2000);

        IReadOnlyList<string> messages = AdministratorModule.BuildLogMessages([line]);

        Assert.Equal(2, messages.Count);
        Assert.All(messages, message => Assert.InRange(message.Length, 1, 2000));
        Assert.Equal(line, string.Concat(messages.Select(UnwrapCodeBlock)));
    }

    [Fact]
    public void BuildLogMessages_GroupsLinesInOrder()
    {
        IReadOnlyList<string> messages = AdministratorModule.BuildLogMessages(["first", "second"]);

        Assert.Equal(["```\nfirst\nsecond\n```"], messages);
    }

    [Fact]
    public void BuildLogMessages_UsesEntireMessageAtPayloadBoundary()
    {
        string line = new('x', 1992);

        string message = Assert.Single(AdministratorModule.BuildLogMessages([line]));

        Assert.Equal(2000, message.Length);
        Assert.Equal(line, UnwrapCodeBlock(message));
    }

    [Fact]
    public void BuildLogMessages_StartsNewChunkAfterPayloadBoundary()
    {
        string fullLine = new('x', 1992);

        IReadOnlyList<string> messages = AdministratorModule.BuildLogMessages([fullLine, "next"]);

        Assert.Equal(2, messages.Count);
        Assert.Equal(fullLine, UnwrapCodeBlock(messages[0]));
        Assert.Equal("next", UnwrapCodeBlock(messages[1]));
    }

    [Fact]
    public void BuildLogMessages_DoesNotSplitSurrogatePairs()
    {
        string line = new string('x', 1991) + "😀tail";

        IReadOnlyList<string> messages = AdministratorModule.BuildLogMessages([line]);
        string[] payloads = messages.Select(UnwrapCodeBlock).ToArray();

        Assert.Equal(line, string.Concat(payloads));
        Assert.All(payloads, payload =>
        {
            Assert.False(char.IsHighSurrogate(payload[^1]));
            Assert.False(char.IsLowSurrogate(payload[0]));
        });
    }

    private static string UnwrapCodeBlock(string message) => message[4..^4];
}