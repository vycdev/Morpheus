using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace Morpheus.Utilities.Images;

public static class ImageMemefier
{
    public static byte[] Memefy(byte[] imageData, string text)
    {
        if (imageData == null || imageData.Length == 0)
            throw new ArgumentNullException(nameof(imageData));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty.", nameof(text));

        using SKBitmap original = SkiaImages.Decode(imageData);
        int maxCaptionHeight = original.Height / 2;
        int horizontalPadding = (int)(original.Width * 0.05f);
        int verticalPadding = (int)(original.Width * 0.03f);
        int textAreaWidth = original.Width - horizontalPadding * 2;
        if (textAreaWidth <= 0 || maxCaptionHeight < 1)
            throw new ArgumentException("Image is too small to add text.");

        float minFontSize = Math.Max(10, original.Width * 0.02f);
        using SKFont font = SkiaImages.CreateFont(Math.Max(minFontSize, original.Width * 0.1f), SKFontStyle.Bold);
        using SKShaper shaper = new(font.Typeface);
        List<string> lines;
        float textHeight;
        int captionHeight;
        while (true)
        {
            lines = SkiaImages.WrapText(text, font, shaper, textAreaWidth);
            textHeight = lines.Count * font.Spacing;
            captionHeight = (int)Math.Ceiling(textHeight) + verticalPadding * 2;
            if (captionHeight <= maxCaptionHeight || font.Size <= minFontSize)
                break;
            font.Size = Math.Max(minFontSize, font.Size * 0.9f);
        }
        captionHeight = Math.Min(captionHeight, maxCaptionHeight);

        using SKBitmap result = new(original.Width, original.Height + captionHeight);
        using SKCanvas canvas = new(result);
        canvas.Clear(SKColors.White);
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, original.Width, captionHeight));
        using SKPaint paint = new() { Color = SKColors.Black, IsAntialias = true };
        float baseline = (captionHeight - textHeight) / 2f - font.Metrics.Ascent;
        foreach (string line in lines)
        {
            canvas.DrawShapedText(shaper, line, horizontalPadding, baseline, SKTextAlign.Left, font, paint);
            baseline += font.Spacing;
        }
        canvas.Restore();
        canvas.DrawBitmap(original, 0, captionHeight, new SKSamplingOptions(SKFilterMode.Nearest));
        return SkiaImages.Encode(result);
    }
}
