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
    [InlineData("emoji_import_page:next", true)]
    [InlineData("emoji_import_page:prev", false)]
    public void TryParseEmojiPageDirection_AcceptsKnownDirections(string customId, bool expectedNext)
    {
        bool parsed = EmojisModule.TryParseEmojiPageDirection(customId, out bool isNext);

        Assert.True(parsed);
        Assert.Equal(expectedNext, isNext);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("emoji_import_page")]
    [InlineData("emoji_import_page:")]
    [InlineData("emoji_import_page:first")]
    [InlineData("emoji_import_page:next:extra")]
    [InlineData("other:next")]
    public void TryParseEmojiPageDirection_RejectsMalformedDirections(string? customId)
    {
        Assert.False(EmojisModule.TryParseEmojiPageDirection(customId, out _));
    }
}
