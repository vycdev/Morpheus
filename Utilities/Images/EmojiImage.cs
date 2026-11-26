using SkiaSharp;

namespace Morpheus.Utilities.Images;

public static class EmojiImage
{
    internal const int MaxUploadBytes = 256 * 1024;
    internal const int MaxDimension = 128;
    private const int MaxDecodedPixels = 25_000_000;

    /// <summary>
    /// Converts an image into data Discord accepts for a custom emoji. Animated GIFs and WebP files
    /// within the upload limit pass through unchanged. Still images become PNGs that fit in 128x128.
    /// </summary>
    internal static bool TryPrepare(byte[] imageData, out byte[] emojiData, out bool animated, out string? error)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        emojiData = [];
        animated = false;

        using SKData data = SKData.CreateCopy(imageData);
        using SKCodec? codec = SKCodec.Create(data);
        if (codec == null)
        {
            error = "That file isn't an image I can read. Use a PNG, JPEG, WebP or GIF.";
            return false;
        }

        if (codec.EncodedFormat is SKEncodedImageFormat.Gif or SKEncodedImageFormat.Webp && codec.FrameCount > 1)
        {
            if (imageData.Length > MaxUploadBytes)
            {
                error = $"Animated images must be {MaxUploadBytes / 1024} KB or smaller to become an emoji.";
                return false;
            }

            animated = true;
            emojiData = imageData;
            error = null;
            return true;
        }

        try
        {
            SKImageInfo info = codec.Info;
            float scale = Math.Min(1f, (float)MaxDimension / Math.Max(info.Width, info.Height));
            SKSizeI decodeSize = codec.GetScaledDimensions(scale);
            if ((long)decodeSize.Width * decodeSize.Height > MaxDecodedPixels)
            {
                error = "That image has too many pixels. Resize it before adding it as an emoji.";
                return false;
            }

            using SKBitmap image = SkiaImages.Decode(codec, decodeSize);
            int width = Math.Max(1, (int)(info.Width * scale));
            int height = Math.Max(1, (int)(info.Height * scale));
            using SKBitmap? resized = image.Width == width && image.Height == height ? null : SkiaImages.Resize(image, width, height);
            emojiData = EncodeOriented(resized ?? image, codec.EncodedOrigin);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            error = "That image could not be processed. Try a different file.";
            return false;
        }

        error = null;
        return true;
    }

    private static byte[] EncodeOriented(SKBitmap image, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
            return SkiaImages.Encode(image);

        int width = image.Width;
        int height = image.Height;
        bool swapDimensions = origin >= SKEncodedOrigin.LeftTop;
        using SKBitmap oriented = new(swapDimensions ? height : width, swapDimensions ? width : height);
        using SKCanvas canvas = new(oriented);
        SKMatrix transform = origin switch
        {
            SKEncodedOrigin.TopRight => new(-1, 0, width, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => SKMatrix.CreateRotationDegrees(180, width / 2f, height / 2f),
            SKEncodedOrigin.BottomLeft => new(1, 0, 0, 0, -1, height, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new(0, -1, height, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new(0, -1, height, -1, 0, width, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new(0, 1, 0, -1, 0, width, 0, 0, 1),
            _ => SKMatrix.Identity
        };
        canvas.Concat(transform);
        canvas.DrawBitmap(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return SkiaImages.Encode(oriented);
    }
}
