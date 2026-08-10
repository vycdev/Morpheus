using Morpheus.Modules;

namespace Morpheus.Tests;

public class ImageModuleTests
{
    [Fact]
    public void TryDownscaleImage_ReturnsFalseForInvalidImageData()
    {
        bool result = ImageModule.TryDownscaleImage([0, 1, 2], out byte[] output);

        Assert.False(result);
        Assert.Empty(output);
    }
}
