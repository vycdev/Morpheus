using Morpheus.Utilities.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Morpheus.Tests;

public class ImageRenderingTests
{
    [Fact]
    public void Memefy_DrawsCaptionAndPreservesOriginalPixels()
    {
        byte[] input = CreateImage(240, 160);

        byte[] output = ImageMemefier.Memefy(input, "Hello from Morpheus");

        using Image<Rgba32> image = Image.Load<Rgba32>(output);
        Assert.Equal(240, image.Width);
        int captionHeight = image.Height - 160;
        Assert.InRange(captionHeight, 1, 80);
        Assert.Equal(new Rgba32(255, 0, 0), image[120, captionHeight + 80]);
        Assert.Equal(new Rgba32(255, 255, 255), image[0, 0]);
        Assert.True(ContainsDarkPixel(image, new Rectangle(0, 0, image.Width, captionHeight)));
    }

    [Fact]
    public void GenerateLineChart_DrawsSeriesLegendAndText()
    {
        byte[] output = ActivityGraphGenerator.GenerateLineChart(
            new Dictionary<string, List<int>> { ["Alice"] = [2, 4, 3], ["Bob"] = [1, 2, 1] },
            days: 3, width: 640, height: 360, start: new DateTime(2026, 1, 1));

        using Image<Rgba32> image = Image.Load<Rgba32>(output);
        Assert.Equal(640, image.Width);
        Assert.Equal(360, image.Height);
        Assert.Equal(Color.ParseHex("#1f77b4").ToPixel<Rgba32>(), image[496, 50]);
        Assert.Equal(Color.ParseHex("#ff7f0e").ToPixel<Rgba32>(), image[496, 72]);
        Assert.True(ContainsDarkPixel(image, new Rectangle(508, 40, 100, 40)));
        Assert.Equal(new Rgba32(255, 255, 255), image[0, 0]);
    }

    [Fact]
    public void GenerateCaptcha_RendersTextAndNoiseAsPng()
    {
        CaptchaResult result = new CaptchaGenerator(width: 320, height: 100, textLength: 4).GenerateCaptcha();

        using Image<Rgba32> image = Image.Load<Rgba32>(result.CaptchaImageBytes);
        Assert.Equal(320, image.Width);
        Assert.Equal(100, image.Height);
        Assert.InRange(result.CaptchaText.Length, 1, 4);
        Assert.True(ContainsDarkPixel(image, image.Bounds));
    }

    [Fact]
    public void DeepFryExtra_PreservesImageDimensions()
    {
        byte[] output = ImageDeepFryer.DeepFryExtra(CreateImage(240, 160));

        using Image<Rgba32> image = Image.Load<Rgba32>(output);
        Assert.Equal(240, image.Width);
        Assert.Equal(160, image.Height);
    }

    private static byte[] CreateImage(int width, int height)
    {
        using Image<Rgba32> image = new(width, height, new Rgba32(255, 0, 0));
        using MemoryStream stream = new();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static bool ContainsDarkPixel(Image<Rgba32> image, Rectangle bounds)
    {
        for (int y = bounds.Top; y < bounds.Bottom; y++)
        for (int x = bounds.Left; x < bounds.Right; x++)
        {
            Rgba32 pixel = image[x, y];
            if (pixel.R < 100 && pixel.G < 100 && pixel.B < 100)
                return true;
        }

        return false;
    }
}
