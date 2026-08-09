using Morpheus.Modules;

namespace Morpheus.Tests;

public class EmojisModuleTests
{
    [Fact]
    public void TryGetReferencedMessageId_ReturnsFalseWhenReferenceIsMissing()
    {
        bool found = EmojisModule.TryGetReferencedMessageId(null, out ulong messageId);

        Assert.False(found);
        Assert.Equal(0UL, messageId);
    }

    [Fact]
    public void TryGetReferencedMessageId_ReturnsReferenceIdWhenPresent()
    {
        bool found = EmojisModule.TryGetReferencedMessageId(42UL, out ulong messageId);

        Assert.True(found);
        Assert.Equal(42UL, messageId);
    }

    [Theory]
    [InlineData("123456789", 123456789UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    public void TryParseSelectionId_AcceptsUnsignedDecimalIds(string value, ulong expected)
    {
        bool parsed = EmojisModule.TryParseSelectionId(value, out ulong id);

        Assert.True(parsed);
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-an-id")]
    [InlineData("-1")]
    [InlineData("18446744073709551616")]
    public void TryParseSelectionId_RejectsMalformedIds(string? value)
    {
        Assert.False(EmojisModule.TryParseSelectionId(value, out _));
    }
}
