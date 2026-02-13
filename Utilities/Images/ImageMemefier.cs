using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;

namespace Morpheus.Utilities.Images;

public static class ImageMemefier
{
    public static byte[] Memefy(byte[] imageData, string text)
    {
        if (imageData == null || imageData.Length == 0)
            throw new ArgumentNullException(nameof(imageData));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty.", nameof(text));

        using MemoryStream ms = new(imageData);
        using Image<Rgba32> original = Image.Load<Rgba32>(ms);

        int imgWidth = original.Width;
        int imgHeight = original.Height;
        int maxCaptionHeight = imgHeight / 2;
        int horizontalPadding = (int)(imgWidth * 0.05f);
        int verticalPadding = (int)(imgWidth * 0.03f);
        int textAreaWidth = imgWidth - horizontalPadding * 2;

        if (textAreaWidth <= 0)
            throw new ArgumentException("Image is too small to add text.");

        if (!SystemFonts.Families.Any())
            throw new InvalidOperationException("No system fonts available.");

        FontFamily fontFamily = SystemFonts.Families.First();

        // Find the largest font size where text fits within the max caption height
        float fontSize = imgWidth * 0.1f;
        float minFontSize = Math.Max(10, imgWidth * 0.02f);
        Font font;
        FontRectangle measured;
        int captionHeight;

        while (true)
        {
            font = fontFamily.CreateFont(fontSize, FontStyle.Bold);
            TextOptions measureOptions = new(font)
            {
                WrappingLength = textAreaWidth,
                Origin = new PointF(0, 0)
            };
            measured = TextMeasurer.MeasureSize(text, measureOptions);
            captionHeight = (int)measured.Height + verticalPadding * 2;

            if (captionHeight <= maxCaptionHeight || fontSize <= minFontSize)
                break;

            fontSize *= 0.9f;
        }

        captionHeight = Math.Min(captionHeight, maxCaptionHeight);

        // Create the final image: white caption bar + original image
        int totalHeight = captionHeight + imgHeight;
        using Image<Rgba32> result = new(imgWidth, totalHeight, Color.White);

        // Draw wrapped text centered vertically in the caption area
        float textX = horizontalPadding;
        float textY = (captionHeight - measured.Height) / 2f;

        RichTextOptions drawOptions = new(font)
        {
            WrappingLength = textAreaWidth,
            Origin = new PointF(textX, textY)
        };

        result.Mutate(ctx => ctx.DrawText(drawOptions, text, Color.Black));

        // Draw the original image below the caption
        result.Mutate(ctx => ctx.DrawImage(original, new Point(0, captionHeight), 1f));

        using MemoryStream outputMs = new();
        result.SaveAsPng(outputMs);
        return outputMs.ToArray();
    }
}
