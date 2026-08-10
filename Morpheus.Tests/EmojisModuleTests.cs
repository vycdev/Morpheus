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
    [InlineData("emoji_import_page")]
    [InlineData("emoji_import_page:")]
    [InlineData("emoji_import_page:first")]
    [InlineData("emoji_import_page:next:extra")]
    [InlineData("other:next")]
    public void TryParseEmojiPageDirection_RejectsMalformedDirections(string? customId)
    {
        Assert.False(EmojisModule.TryParseEmojiPageDirection(customId, out _));
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

    [Fact]
    public void GetEmojiArchivePath_UsesGuildIdUnderTempDirectory()
    {
        string tempPath = Path.Combine("tmp", "morpheus-tests");

        string archivePath = EmojisModule.GetEmojiArchivePath(tempPath, 123UL);

        Assert.Equal(Path.Combine(tempPath, "Morpheus_Emojis_123.zip"), archivePath);
    }
}
