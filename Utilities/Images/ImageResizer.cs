using SkiaSharp;

namespace Morpheus.Utilities.Images;

public static class ImageResizer
{
    private const int MaxDimension = 3840;

    /// <summary>
    /// Downscales an image if either dimension exceeds 4K (3840px), preserving aspect ratio.
    /// Returns the original bytes unchanged if already within limits.
    /// </summary>
    public static byte[] DownscaleIfTooLarge(byte[] imageData)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        using SKData data = SKData.CreateCopy(imageData);
        using SKCodec codec = SKCodec.Create(data)
            ?? throw new ArgumentException("Unsupported or invalid image data.", nameof(imageData));
        SKImageInfo info = codec.Info;
        if (info.Width <= MaxDimension && info.Height <= MaxDimension)
            return imageData;

        float scale = Math.Min((float)MaxDimension / info.Width, (float)MaxDimension / info.Height);
        int width = Math.Max(1, (int)(info.Width * scale));
        int height = Math.Max(1, (int)(info.Height * scale));
        using SKBitmap image = SkiaImages.Decode(imageData);
        using SKBitmap resized = SkiaImages.Resize(image, width, height);
        return SkiaImages.Encode(resized);
    }
}
