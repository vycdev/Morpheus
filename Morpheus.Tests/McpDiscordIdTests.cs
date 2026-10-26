using Morpheus.MCP;

namespace Morpheus.Tests;

public class McpDiscordIdTests
{
    [Theory]
    [InlineData("1", 1UL)]
    [InlineData("00000000000000000001", 1UL)]
    [InlineData("900000000000003", 900000000000003UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    public void Parse_AcceptsPositiveValuesAcrossTheUInt64Range(string value, ulong expected)
    {
        Assert.Equal(expected, McpDiscordId.Parse(value, "id"));
        Assert.Equal(expected, McpDiscordId.ParseOptional(value, "id"));
    }

    [Fact]
    public void ParseOptional_AllowsAnOmittedId() =>
        Assert.Null(McpDiscordId.ParseOptional(null, "id"));

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("00000000000000000000")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData("١")]
    [InlineData("18446744073709551616")]
    [InlineData("000000000000000000001")]
    public void Parse_RejectsMalformedOrOutOfRangeIds(string value)
    {
        ArgumentException required = Assert.Throws<ArgumentException>(() => McpDiscordId.Parse(value, "guildId"));
        Assert.Equal("guildId", required.ParamName);

        ArgumentException optional = Assert.Throws<ArgumentException>(() => McpDiscordId.ParseOptional(value, "channelId"));
        Assert.Equal("channelId", optional.ParamName);
    }
}
