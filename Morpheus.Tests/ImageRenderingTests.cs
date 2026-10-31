using Morpheus.Utilities.Images;
using SkiaSharp;

namespace Morpheus.Tests;

public class ImageRenderingTests
{
    // Independent one-pixel GIF, BMP, and PNG-backed ICO fixtures exercise decoders, not our encoder.
    [Theory]
    [InlineData("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7")]
    [InlineData("Qk06AAAAAAAAADYAAAAoAAAAAQAAAAEAAAABABgAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAD/AA==")]
    [InlineData("AAABAAEAAQEAAAEAIABFAAAAFgAAAIlQTkcNChoKAAAADUlIRFIAAAABAAAAAQgCAAAAkHdT3gAAAAxJREFUeJxj+M/AAAADAQEAyf6S7wAAAABJRU5ErkJggg==")]
    public void ImageCommands_AcceptGifBmpAndIco(string fixture)
    {
        byte[] input = Convert.FromBase64String(fixture);
        Assert.Same(input, ImageResizer.DownscaleIfTooLarge(input));
        using SKBitmap result = SKBitmap.Decode(Blurpifier.Blurpify(input, seed: 123));
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
    }

    [Fact]
    public void ImageCommands_RejectTiffWithManagedError()
    {
        // Valid uncompressed, one-pixel grayscale TIFF; Skia does not provide a TIFF codec.
        byte[] input = Convert.FromBase64String("SUkqAAgAAAAJAAABAwABAAAAAQAAAAEBAwABAAAAAQAAAAIBAwABAAAACAAAAAMBAwABAAAAAQAAAAYBAwABAAAAAQAAABEBBAABAAAAegAAABUBAwABAAAAAQAAABYBBAABAAAAAQAAABcBBAABAAAAAQAAAAAAAAD/");
        Assert.Throws<ArgumentException>(() => ImageResizer.DownscaleIfTooLarge(input));
        Assert.Throws<ArgumentException>(() => ImageDeepFryer.DeepFry(input));
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png, SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg, SKEncodedImageFormat.Jpeg)]
    [InlineData(SKEncodedImageFormat.Webp, SKEncodedImageFormat.Png)]
    public void DeepFry_AcceptsCommonFormatsAndKeepsDimensions(SKEncodedImageFormat inputFormat, SKEncodedImageFormat outputFormat)
    {
        byte[] result = ImageDeepFryer.DeepFry(CreateImage(format: inputFormat));
        using SKData data = SKData.CreateCopy(result);
        using SKCodec codec = SKCodec.Create(data);
        Assert.Equal(outputFormat, codec.EncodedFormat);
        Assert.Equal(320, codec.Info.Width);
        Assert.Equal(200, codec.Info.Height);
    }

    [Fact]
    public void DeepFry_WithNeutralSettingsPreservesColorsAndTransparency()
    {
        using SKBitmap source = new(3, 1);
        source.SetPixel(0, 0, new SKColor(90, 150, 210));
        source.SetPixel(1, 0, new SKColor(255, 0, 0, 128));
        source.SetPixel(2, 0, SKColors.Transparent);
        using SKData input = source.Encode(SKEncodedImageFormat.Png, 100);
        using SKBitmap result = SKBitmap.Decode(ImageDeepFryer.DeepFry(input.ToArray(), 1, 1, 0));

        Assert.Equal(source.Pixels, result.Pixels);
    }

    [Fact]
    public void DeepFry_ExplicitCompressionProducesJpegFromPng()
    {
        byte[] result = ImageDeepFryer.DeepFry(CreateImage(), jpegQuality: 10);
        Assert.Equal(new byte[] { 0xff, 0xd8 }, result[..2]);
        using SKBitmap image = SKBitmap.Decode(result);
        Assert.Equal(320, image.Width);
    }

    [Theory]
    [InlineData(320, 200)]
    [InlineData(1, 1)]
    public void DeepFryExtra_ProducesJpegEvenForTinyImages(int width, int height)
    {
        byte[] result = ImageDeepFryer.DeepFryExtra(CreateImage(width, height));
        Assert.Equal(new byte[] { 0xff, 0xd8 }, result[..2]);
        using SKBitmap image = SKBitmap.Decode(result);
        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
    }

    [Fact]
    public void Blurpify_WithNoEffectsPreservesPixelsAndAlpha()
    {
        byte[] input = CreateImage();
        using SKBitmap source = SKBitmap.Decode(input);
        using SKBitmap result = SKBitmap.Decode(Blurpifier.Blurpify(input, pixelScale: 1, maxOffset: 0, seed: 123));
        Assert.Equal(source.Pixels, result.Pixels);
    }

    [Fact]
    public void Blurpify_IsReproducibleWithSeedAndChangesTheImage()
    {
        byte[] input = CreateImage();
        byte[] first = Blurpifier.Blurpify(input, seed: 123);
        Assert.Equal(first, Blurpifier.Blurpify(input, seed: 123));
        Assert.NotEqual(first, Blurpifier.Blurpify(input, seed: 456));
        using SKBitmap source = SKBitmap.Decode(input);
        using SKBitmap result = SKBitmap.Decode(first);
        Assert.Equal(source.Width, result.Width);
        Assert.Equal(source.Height, result.Height);
        Assert.False(source.Pixels.SequenceEqual(result.Pixels));
    }

    [Theory]
    [InlineData("A caption that wraps onto several lines")]
    [InlineData("SUPERCALIFRAGILISTICEXPIALIDOCIOUSSUPERCALIFRAGILISTICEXPIALIDOCIOUS")]
    [InlineData("First line\nSecond line")]
    [InlineData("مرحبا بالعالم")]
    [InlineData("Cafe\u0301")]
    public void Memefy_DrawsCaptionAboveUnchangedOriginal(string caption)
    {
        using SKBitmap original = new(320, 200);
        original.Erase(SKColors.CornflowerBlue);
        using SKData input = original.Encode(SKEncodedImageFormat.Png, 100);
        using SKBitmap result = SKBitmap.Decode(ImageMemefier.Memefy(input.ToArray(), caption));
        int captionHeight = result.Height - original.Height;
        Assert.Equal(original.Width, result.Width);
        Assert.InRange(captionHeight, 1, original.Height / 2);
        SKColor[] pixels = result.Pixels;
        Assert.Contains(pixels.Take(result.Width * captionHeight), c => c.Red < 100 && c.Green < 100 && c.Blue < 100);
        Assert.Contains(SKColors.White, pixels.Take(result.Width * captionHeight));
        Assert.All(pixels.Skip(result.Width * captionHeight), c => Assert.Equal(SKColors.CornflowerBlue, c));
    }

    [Fact]
    public void Memefy_PreservesTransparentAndSemiTransparentSourcePixels()
    {
        using SKBitmap original = new(320, 200);
        original.Erase(SKColors.Transparent);
        original.SetPixel(0, 0, new SKColor(255, 0, 0, 128));
        using SKData input = original.Encode(SKEncodedImageFormat.Png, 100);

        using SKBitmap result = SKBitmap.Decode(ImageMemefier.Memefy(input.ToArray(), "Hi"));
        int captionHeight = result.Height - original.Height;

        Assert.InRange(captionHeight, 1, original.Height / 2);
        Assert.Equal(original.GetPixel(0, 0), result.GetPixel(0, captionHeight));
        Assert.Equal((byte)0, result.GetPixel(1, captionHeight).Alpha);
    }

    [Fact]
    public void Captcha_RendersTextWithLinuxFontFallback()
    {
        CaptchaResult result = new CaptchaGenerator().GenerateCaptcha();
        Assert.InRange(result.CaptchaText.Length, 1, 8);
        Assert.All(result.CaptchaText, c => Assert.Contains(c, "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789"));
        using SKBitmap image = SKBitmap.Decode(result.CaptchaImageBytes);
        Assert.Equal(230, image.Width);
        Assert.Equal(70, image.Height);
        // More dark pixels than noise alone should produce: missing fonts must not pass silently.
        Assert.True(image.Pixels.Count(c => c.Red < 120 && c.Green < 120 && c.Blue < 120) > 200);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void ActivityChart_RendersSeriesAndLabelsIncludingEmptySeries(int days)
    {
        byte[] result = ActivityGraphGenerator.GenerateLineChart(new()
        {
            ["Alice"] = [2, 4, 1, 8, 3, 6, 5],
            ["Bob"] = []
        }, days, start: new DateTime(2026, 10, 1));
        using SKBitmap image = SKBitmap.Decode(result);
        Assert.Equal(1000, image.Width);
        Assert.Equal(520, image.Height);
        Assert.Equal(SKColors.White, image.GetPixel(0, 0));
        Assert.Contains(SKColor.Parse("#1f77b4"), image.Pixels);
        Assert.Contains(SKColor.Parse("#ff7f0e"), image.Pixels);
        Assert.Contains(SKColors.Black, image.Pixels);
    }

    [Fact]
    public void ImageCommands_RejectInvalidData()
    {
        byte[] input = [1, 2, 3, 4];
        Assert.Throws<ArgumentException>(() => ImageResizer.DownscaleIfTooLarge(input));
        Assert.Throws<ArgumentException>(() => ImageDeepFryer.DeepFry(input));
        Assert.Throws<ArgumentException>(() => Blurpifier.Blurpify(input));
        Assert.Throws<ArgumentException>(() => ImageMemefier.Memefy(input, "caption"));
    }

    private static byte[] CreateImage(int width = 320, int height = 200, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using SKBitmap image = new(width, height);
        using SKCanvas canvas = new(image);
        canvas.Clear(SKColors.Transparent);
        using SKPaint paint = new() { Color = SKColors.CornflowerBlue };
        canvas.DrawRect(0, 0, width / 2f, height, paint);
        paint.Color = SKColors.OrangeRed;
        canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paint);
        using SKData data = image.Encode(format, 100);
        return data.ToArray();
    }
}
