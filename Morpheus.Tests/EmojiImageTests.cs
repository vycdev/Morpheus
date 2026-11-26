using Morpheus.Utilities.Images;
using SkiaSharp;

namespace Morpheus.Tests;

public class EmojiImageTests
{
    [Theory]
    [InlineData(512, 256, 128, 64)]
    [InlineData(64, 300, 27, 128)]
    [InlineData(48, 32, 48, 32)]
    public void TryPrepare_FitsStaticImagesInsideEmojiBounds(int width, int height, int expectedWidth, int expectedHeight)
    {
        byte[] input = EncodeSolid(width, height, SKEncodedImageFormat.Jpeg);

        Assert.True(EmojiImage.TryPrepare(input, out byte[] output, out bool animated, out string? error));

        Assert.False(animated);
        Assert.Null(error);
        using SKCodec codec = SKCodec.Create(new MemoryStream(output));
        Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
        Assert.Equal(expectedWidth, codec.Info.Width);
        Assert.Equal(expectedHeight, codec.Info.Height);
    }

    [Fact]
    public void TryPrepare_PassesSmallAnimatedGifsThroughUnchanged()
    {
        byte[] input = BuildAnimatedGif(paddingBytes: 0);

        Assert.True(EmojiImage.TryPrepare(input, out byte[] output, out bool animated, out string? error));

        Assert.True(animated);
        Assert.Null(error);
        Assert.Same(input, output);
    }

    [Fact]
    public void TryPrepare_RejectsAnimatedGifsOverTheUploadLimit()
    {
        byte[] input = BuildAnimatedGif(paddingBytes: EmojiImage.MaxUploadBytes);

        Assert.False(EmojiImage.TryPrepare(input, out _, out _, out string? error));

        Assert.Contains("256 KB", error);
    }

    [Fact]
    public void TryPrepare_RejectsDataThatIsNotAnImage()
    {
        Assert.False(EmojiImage.TryPrepare("not an image"u8.ToArray(), out _, out _, out string? error));

        Assert.NotNull(error);
    }

    private static byte[] EncodeSolid(int width, int height, SKEncodedImageFormat format)
    {
        using SKBitmap image = new(width, height);
        image.Erase(SKColors.Red);
        using SKData data = image.Encode(format, 90);
        return data.ToArray();
    }

    // Skia cannot encode GIFs, so build a two-frame 1x1 GIF by hand. Comment extensions pad the
    // file size without changing the frames.
    private static byte[] BuildAnimatedGif(int paddingBytes)
    {
        List<byte> gif = [.. "GIF89a"u8.ToArray(), 1, 0, 1, 0, 0x80, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF];
        for (int frame = 0; frame < 2; frame++)
        {
            gif.AddRange([0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00]);
            gif.AddRange([0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0x00]);
            gif.AddRange([0x02, 0x02, 0x44, 0x01, 0x00]);
        }

        for (int remaining = paddingBytes; remaining > 0; remaining -= 255 * 255)
        {
            gif.AddRange([0x21, 0xFE]);
            for (int block = 0; block < 255; block++)
            {
                gif.Add(255);
                gif.AddRange(new byte[255]);
            }
            gif.Add(0);
        }

        gif.Add(0x3B);
        return [.. gif];
    }
}
