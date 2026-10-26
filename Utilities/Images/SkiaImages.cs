using System.Globalization;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace Morpheus.Utilities.Images;

internal static class SkiaImages
{
    internal static SKBitmap Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using SKData encoded = SKData.CreateCopy(data);
        using SKCodec codec = SKCodec.Create(encoded)
            ?? throw new ArgumentException("Unsupported or invalid image data.", nameof(data));
        SKBitmap bitmap = new(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
        {
            bitmap.Dispose();
            throw new ArgumentException("Could not decode the image.", nameof(data));
        }
        return bitmap;
    }

    internal static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 90)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(format, quality)
            ?? throw new InvalidOperationException($"Could not encode image as {format}.");
        return data.ToArray();
    }

    internal static SKBitmap Resize(SKBitmap bitmap, int width, int height, bool nearest = false) =>
        bitmap.Resize(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul),
            nearest ? new SKSamplingOptions(SKFilterMode.Nearest) : new SKSamplingOptions(SKCubicResampler.Mitchell))
        ?? throw new InvalidOperationException("Could not resize image.");

    internal static SKFont CreateFont(float size, SKFontStyle? style = null, string family = "DejaVu Sans")
    {
        using SKTypeface typeface = SKFontManager.Default.MatchFamily(family, style ?? SKFontStyle.Normal)
            ?? SKFontManager.Default.MatchFamily("DejaVu Sans", style ?? SKFontStyle.Normal)
            ?? SKFontManager.Default.MatchFamily(null, style ?? SKFontStyle.Normal)
            ?? SKFontManager.Default.MatchFamily(SKFontManager.Default.FontFamilies.FirstOrDefault(), style ?? SKFontStyle.Normal)
            ?? throw new InvalidOperationException("No system fonts available. Install a TTF font such as DejaVu Sans.");
        return new SKFont(typeface, size) { Edging = SKFontEdging.Antialias };
    }

    // Wrap at words where possible; split long tokens at Unicode text element boundaries.
    internal static List<string> WrapText(string text, SKFont font, SKShaper shaper, float width)
    {
        List<string> lines = [];
        foreach (string paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = line.Length == 0 ? word : $"{line} {word}";
                if (shaper.Shape(candidate, font).Width <= width)
                {
                    line = candidate;
                    continue;
                }
                if (line.Length > 0) lines.Add(line);
                line = "";
                TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(word);
                while (elements.MoveNext())
                {
                    string element = elements.GetTextElement();
                    if (line.Length > 0 && shaper.Shape(line + element, font).Width > width)
                    {
                        lines.Add(line);
                        line = "";
                    }
                    line += element;
                }
            }
            lines.Add(line);
        }
        return lines;
    }
}
