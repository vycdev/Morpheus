using SkiaSharp;

namespace Morpheus.Utilities.Images;

public static class EmojiImage
{
    internal const int MaxUploadBytes = 256 * 1024;
    internal const int MaxDimension = 128;

    /// <summary>
    /// Converts an image into data Discord accepts for a custom emoji. Animated GIFs within the
    /// upload limit pass through unchanged; everything else becomes a PNG that fits in 128x128.
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

        // Skia cannot encode GIFs, so animated emojis must already fit Discord's upload limit.
        if (codec.EncodedFormat == SKEncodedImageFormat.Gif && codec.FrameCount > 1)
        {
            if (imageData.Length > MaxUploadBytes)
            {
                error = $"Animated GIFs must be {MaxUploadBytes / 1024} KB or smaller to become an emoji.";
                return false;
            }

            animated = true;
            emojiData = imageData;
            error = null;
            return true;
        }

        try
        {
            using SKBitmap image = SkiaImages.Decode(imageData);
            float scale = Math.Min(1f, Math.Min((float)MaxDimension / image.Width, (float)MaxDimension / image.Height));
            if (scale < 1f)
            {
                int width = Math.Max(1, (int)(image.Width * scale));
                int height = Math.Max(1, (int)(image.Height * scale));
                using SKBitmap resized = SkiaImages.Resize(image, width, height);
                emojiData = SkiaImages.Encode(resized);
            }
            else
            {
                emojiData = SkiaImages.Encode(image);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            error = "That image could not be processed. Try a different file.";
            return false;
        }

        error = null;
        return true;
    }
}
