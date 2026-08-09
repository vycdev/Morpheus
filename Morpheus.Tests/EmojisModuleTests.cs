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

    [Fact]
    public void GetEmojiArchivePath_UsesGuildIdUnderTempDirectory()
    {
        string tempPath = Path.Combine("tmp", "morpheus-tests");

        string archivePath = EmojisModule.GetEmojiArchivePath(tempPath, 123UL);

        Assert.Equal(Path.Combine(tempPath, "Morpheus_Emojis_123.zip"), archivePath);
    }
}
