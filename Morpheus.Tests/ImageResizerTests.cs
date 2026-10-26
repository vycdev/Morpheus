using Morpheus.Utilities.Images;
using SkiaSharp;

namespace Morpheus.Tests;

public class ImageResizerTests
{
    [Theory]
    [InlineData(32, 16)]
    [InlineData(3840, 1)]
    public void DownscaleIfTooLarge_PassesThroughImagesWithinDimensionLimit(int width, int height)
    {
        byte[] input;
        using (SKBitmap image = new(width, height))
        {
            image.Erase(SKColors.Red);
            using SKData data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            input = data.ToArray();
        }

        byte[] output = ImageResizer.DownscaleIfTooLarge(input);

        // A no-op should not re-encode a JPEG as PNG or allocate a replacement buffer.
        Assert.Same(input, output);
    }

    [Theory]
    [InlineData(1, 10000, 1, 3840)]
    [InlineData(10000, 1, 3840, 1)]
    public void DownscaleIfTooLarge_PreservesAtLeastOnePixelPerDimension(
        int width,
        int height,
        int expectedWidth,
        int expectedHeight)
    {
        byte[] input;
        using (SKBitmap image = new(width, height))
        {
            image.Erase(SKColors.Red);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 90);
            input = data.ToArray();
        }

        byte[] output = ImageResizer.DownscaleIfTooLarge(input);

        using SKBitmap resized = SKBitmap.Decode(output);
        Assert.Equal(expectedWidth, resized.Width);
        Assert.Equal(expectedHeight, resized.Height);
    }
}