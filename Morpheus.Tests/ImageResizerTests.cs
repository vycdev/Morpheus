using Morpheus.Utilities.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Morpheus.Tests;

public class ImageResizerTests
{
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