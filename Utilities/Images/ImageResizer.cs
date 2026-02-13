using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
        using MemoryStream ms = new(imageData);
        ImageInfo info = Image.Identify(ms);

        if (info.Width <= MaxDimension && info.Height <= MaxDimension)
            return imageData;

        ms.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(ms);

        float scale = Math.Min((float)MaxDimension / info.Width, (float)MaxDimension / info.Height);
        int newWidth = (int)(info.Width * scale);
        int newHeight = (int)(info.Height * scale);

        image.Mutate(x => x.Resize(newWidth, newHeight));

        using MemoryStream output = new();
        image.SaveAsPng(output);
        return output.ToArray();
    }
}
