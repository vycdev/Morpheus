using Morpheus.Utilities.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Morpheus.Tests;

public class ImageResizerTests
{
    [Theory]
    [InlineData(32, 16)]
    [InlineData(3840, 1)]
    public void DownscaleIfTooLarge_PassesThroughImagesWithinDimensionLimit(int width, int height)
    {
        byte[] input;
        using (Image<Rgba32> image = new(width, height, new Rgba32(255, 0, 0)))
        using (MemoryStream stream = new())
        {
            image.SaveAsJpeg(stream);
            input = stream.ToArray();
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
        using (Image<Rgba32> image = new(width, height, new Rgba32(255, 0, 0)))
        using (MemoryStream stream = new())
        {
            image.SaveAsPng(stream);
            input = stream.ToArray();
        }

        byte[] output = ImageResizer.DownscaleIfTooLarge(input);

        using Image<Rgba32> resized = Image.Load<Rgba32>(output);
        Assert.Equal(expectedWidth, resized.Width);
        Assert.Equal(expectedHeight, resized.Height);
    }
}