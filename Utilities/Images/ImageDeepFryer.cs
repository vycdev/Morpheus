using SkiaSharp;

namespace Morpheus.Utilities.Images;

public static class ImageDeepFryer
{
    // Directory containing pre-rendered Twemoji PNG files (72x72)
    private static readonly string EmojiImageDir =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Emojis");

    // Cached list of emoji image paths, loaded once on first use
    private static readonly Lazy<string[]> _emojiImages = new(() =>
        Directory.Exists(EmojiImageDir)
            ? Directory.GetFiles(EmojiImageDir, "*.png")
            : []);

    private static readonly string[] ShitpostTexts =
    [
        // Phrases
        "BRUH", "LMAO", "WHO DID THIS", "IM DEAD", "BOTTOM TEXT",
        "NO CAP", "FR FR", "NAH BRO", "SHEESH", "AYO?!", "REAL",
        "NPC BEHAVIOR", "CAUGHT IN 4K", "EMOTIONAL DAMAGE", "SUS",
        "ZAMN", "SKILL ISSUE", "CRYING RN", "BRUH MOMENT",
        "GONE WRONG", "NOT CLICKBAIT", "LITERALLY SHAKING",
        "I CANT", "HELP", "WHO ASKED", "RATIO", "COPE",
        "SEETHE", "TOUCH GRASS", "ITS GIVING", "NO SHOT",
        "WHAT THE SIGMA", "SKIBIDI", "GYATT",
        // Text emojis
        "XD", "B", ":D", "100", "OOF", ":3", "UwU", "OwO", ">:)", "lol",
        "B)", "<3", "o_O", "D:", "X_X", ":P", ";)", ":O", "xD", "^^",
        ":^)", "._.", "-_-", ">_<", "T_T", ":V", "c:", ":c", "0_0", ">:("
    ];

    private static readonly SKColor[] BrightColors =
    [
        SKColors.White, SKColors.Yellow, SKColors.Cyan, SKColors.Magenta, SKColors.Lime,
        SKColors.Gold, SKColors.GreenYellow, SKColors.Aqua, SKColors.Chartreuse,
        new SKColor(255, 255, 100), new SKColor(255, 100, 255),
        new SKColor(100, 255, 255), new SKColor(255, 200, 50),
        new SKColor(200, 255, 50), new SKColor(255, 150, 200)
    ];

    /// <summary>
    /// Applies a "deep-fried" effect to an image.
    /// </summary>
    /// <param name="imageData">The byte array representing the original image.</param>
    /// <param name="contrastFactor">Factor for increasing contrast (e.g., 2.0 to 4.0). Higher is more contrast.</param>
    /// <param name="saturationFactor">Factor for increasing saturation (e.g., 1.5 to 3.0). Higher is more saturated.</param>
    /// <param name="noiseAmount">Amount of noise to add (e.g., 20 to 50). Max 255.</param>
    /// <returns>A byte array representing the deep-fried image.</returns>
    public static byte[] DeepFry(byte[] imageData, double contrastFactor = 1.8, float saturationFactor = 1.5f, int noiseAmount = 25, int jpegQuality = 0)
    {
        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentNullException(nameof(imageData));
        }

        try
        {
            using SKData encoded = SKData.CreateCopy(imageData);
            using SKCodec codec = SKCodec.Create(encoded)
                ?? throw new ArgumentException("Unsupported or invalid image data.", nameof(imageData));
            SKEncodedImageFormat originalFormat = codec.EncodedFormat;
            using SKBitmap input = SkiaImages.Decode(imageData);
            int width = input.Width;
            int height = input.Height;
            using SKBitmap output = new(width, height);
            SKColor[] pixels = input.Pixels;

            Random random = new();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    SKColor orig = pixels[y * width + x];

                    // 1. Contrast
                    double rC = (orig.Red / 255.0 - 0.5) * contrastFactor + 0.5;
                    double gC = (orig.Green / 255.0 - 0.5) * contrastFactor + 0.5;
                    double bC = (orig.Blue / 255.0 - 0.5) * contrastFactor + 0.5;

                    float rContrast = (float)(rC * 255.0);
                    float gContrast = (float)(gC * 255.0);
                    float bContrast = (float)(bC * 255.0);

                    // 2. Saturation (approx)
                    float L = 0.299f * rContrast + 0.587f * gContrast + 0.114f * bContrast;
                    float rSat = L + saturationFactor * (rContrast - L);
                    float gSat = L + saturationFactor * (gContrast - L);
                    float bSat = L + saturationFactor * (bContrast - L);

                    // 3. Noise
                    int rN = ClampToByte(rSat + random.Next(-noiseAmount, noiseAmount + 1));
                    int gN = ClampToByte(gSat + random.Next(-noiseAmount, noiseAmount + 1));
                    int bN = ClampToByte(bSat + random.Next(-noiseAmount, noiseAmount + 1));

                    pixels[y * width + x] = new SKColor((byte)rN, (byte)gN, (byte)bN, orig.Alpha);
                }
            }

            output.Pixels = pixels;
            if (jpegQuality > 0 && jpegQuality < 100)
                return SkiaImages.Encode(output, SKEncodedImageFormat.Jpeg, jpegQuality);
            return SkiaImages.Encode(output, originalFormat == SKEncodedImageFormat.Jpeg
                ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png);

        }
        catch (Exception ex)
        {
            throw new ArgumentException("Invalid image data or processing failed.", nameof(imageData), ex);
        }
    }

    private static byte ClampToByte(double val)
    {
        if (val < 0) return 0;
        if (val > 255) return 255;
        return (byte)val;
    }
    private static byte ClampToByte(float val)
    {
        if (val < 0) return 0;
        if (val > 255) return 255;
        return (byte)val;
    }

    /// <summary>
    /// Applies an extra deep-fried effect with a random emoji and shitpost phrase overlaid,
    /// plus a spherical bulge warp and vignette.
    /// </summary>
    public static byte[] DeepFryExtra(byte[] imageData)
    {
        byte[] friedData = DeepFry(imageData, contrastFactor: 2.5, saturationFactor: 2.0f, noiseAmount: 35, jpegQuality: 10);

        using SKBitmap image = SkiaImages.Decode(friedData);

        Random random = new();
        int width = image.Width;
        int height = image.Height;

        // 1. Overlays (before effects so they get distorted too)
        (SKRectI regionA, SKRectI regionB) = GetPlacementRegions(width, height, random);
        bool emojiFirst = random.Next(2) == 0;
        SKRectI emojiRegion = emojiFirst ? regionA : regionB;
        SKRectI textRegion = emojiFirst ? regionB : regionA;

        // Emoji overlay from pre-rendered Twemoji PNGs
        string[] emojiFiles = _emojiImages.Value;
        if (emojiFiles.Length > 0)
            TryOverlayEmojiImage(image, emojiFiles[random.Next(emojiFiles.Length)], random, emojiRegion);

        // Text overlay (phrases + text emojis)
        string text = ShitpostTexts[random.Next(ShitpostTexts.Length)];
        TryOverlayFittedText(image, text, random, textRegion);

        // 2. Random distortion: bulge or twist
        if (random.Next(2) == 0)
            ApplyBulge(image, random);
        else
            ApplyTwist(image, random);

        // 3. Vignette: darken toward the corners while retaining the center.
        SKColor[] pixels = image.Pixels;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float dx = (x - (width - 1) / 2f) / Math.Max(1, width * 0.9f);
            float dy = (y - (height - 1) / 2f) / Math.Max(1, height * 0.9f);
            float brightness = 1 - Math.Clamp(dx * dx + dy * dy, 0, 0.85f);
            SKColor color = pixels[y * width + x];
            pixels[y * width + x] = new SKColor((byte)(color.Red * brightness), (byte)(color.Green * brightness),
                (byte)(color.Blue * brightness), color.Alpha);
        }
        image.Pixels = pixels;
        return SkiaImages.Encode(image, SKEncodedImageFormat.Jpeg, 15);
    }

    private static void TryOverlayEmojiImage(SKBitmap target, string emojiPath, Random random, SKRectI region)
    {
        if (region.Width <= 0 || region.Height <= 0) return;
        try
        {
            using SKBitmap emoji = SkiaImages.Decode(File.ReadAllBytes(emojiPath));
            float targetSize = region.Height * (0.3f + (float)random.NextDouble() * 0.3f);
            float scale = targetSize / Math.Max(emoji.Width, emoji.Height);
            Overlay(target, emoji, random, region, emoji.Width * scale, emoji.Height * scale);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEEPFRY] Emoji overlay failed for {emojiPath}: {ex.Message}");
        }
    }

    private static void TryOverlayFittedText(SKBitmap target, string text, Random random, SKRectI region)
    {
        if (region.Width <= 0 || region.Height <= 0) return;
        try
        {
            float size = Math.Max(12, region.Height * (0.3f + (float)random.NextDouble() * 0.3f));
            SKFontStyle style = random.Next(3) switch { 0 => SKFontStyle.Bold, 1 => SKFontStyle.Italic, _ => SKFontStyle.Normal };
            using SKFont font = SkiaImages.CreateFont(size, style);
            font.MeasureText(text, out SKRect bounds);
            float padding = Math.Max(5, size / 10);
            using SKBitmap layer = new(Math.Max(1, (int)Math.Ceiling(bounds.Width + padding * 2)),
                Math.Max(1, (int)Math.Ceiling(bounds.Height + padding * 2)));
            using SKCanvas canvas = new(layer);
            canvas.Clear(SKColors.Transparent);
            using SKPaint paint = new() { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1, size / 20) };
            canvas.DrawText(text, padding - bounds.Left, padding - bounds.Top, SKTextAlign.Left, font, paint);
            paint.Style = SKPaintStyle.Fill;
            paint.Color = BrightColors[random.Next(BrightColors.Length)];
            canvas.DrawText(text, padding - bounds.Left, padding - bounds.Top, SKTextAlign.Left, font, paint);
            Overlay(target, layer, random, region, layer.Width, layer.Height);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEEPFRY] Text overlay failed for \"{text}\": {ex.Message}");
        }
    }

    private static void Overlay(SKBitmap target, SKBitmap layer, Random random, SKRectI region, float width, float height)
    {
        float w = Math.Clamp(width * (0.6f + (float)random.NextDouble() * 1.5f), 1, region.Width);
        float h = Math.Clamp(height * (0.6f + (float)random.NextDouble() * 1.5f), 1, region.Height);
        float angle = (float)(random.NextDouble() * 40 - 20);
        float sin = MathF.Abs(MathF.Sin(angle * MathF.PI / 180));
        float cos = MathF.Abs(MathF.Cos(angle * MathF.PI / 180));
        float rotatedW = w * cos + h * sin;
        float rotatedH = h * cos + w * sin;
        float x = region.Left + (float)random.NextDouble() * Math.Max(0, region.Width - rotatedW);
        float y = region.Top + (float)random.NextDouble() * Math.Max(0, region.Height - rotatedH);
        using SKCanvas canvas = new(target);
        canvas.Translate(x + rotatedW / 2, y + rotatedH / 2);
        canvas.RotateDegrees(angle);
        // Brightness 1.5 followed by contrast 1.5, preserving alpha.
        using SKColorFilter boost = SKColorFilter.CreateColorMatrix([
            2.25f, 0, 0, 0, -0.25f,
            0, 2.25f, 0, 0, -0.25f,
            0, 0, 2.25f, 0, -0.25f,
            0, 0, 0, 1, 0
        ]);
        using SKPaint paint = new() { IsAntialias = true, ColorFilter = boost,
            Color = SKColors.White.WithAlpha((byte)(230 + random.Next(26))) };
        canvas.DrawBitmap(layer, new SKRect(-w / 2, -h / 2, w / 2, h / 2),
            new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
    }

    private static (SKRectI, SKRectI) GetPlacementRegions(int width, int height, Random random)
    {
        int style = random.Next(4);
        return style switch
        {
            // Top / bottom halves
            0 => (SKRectI.Create(0, 0, width, height / 2),
                  SKRectI.Create(0, height / 2, width, height / 2)),
            // Left / right halves
            1 => (SKRectI.Create(0, 0, width / 2, height),
                  SKRectI.Create(width / 2, 0, width / 2, height)),
            // Top-left corner / bottom-right corner
            2 => (SKRectI.Create(0, 0, (int)(width * 0.45), (int)(height * 0.45)),
                  SKRectI.Create((int)(width * 0.55), (int)(height * 0.55), (int)(width * 0.45), (int)(height * 0.45))),
            // Top-right corner / bottom-left corner
            _ => (SKRectI.Create((int)(width * 0.55), 0, (int)(width * 0.45), (int)(height * 0.45)),
                  SKRectI.Create(0, (int)(height * 0.55), (int)(width * 0.45), (int)(height * 0.45))),
        };
    }

    private static void ApplyBulge(SKBitmap image, Random random)
    {
        int width = image.Width;
        int height = image.Height;

        // Random center biased toward the middle area
        int cx = width / 4 + random.Next(width / 2);
        int cy = height / 4 + random.Next(height / 2);

        // Radius: 15-30% of the smaller dimension
        int minDim = Math.Min(width, height);
        int radius = (int)(minDim * (0.15 + random.NextDouble() * 0.15));
        if (radius < 10) return;

        float strength = 0.4f + (float)(random.NextDouble() * 0.4); // 0.4 to 0.8

        SKColor[] source = image.Pixels;
        SKColor[] output = (SKColor[])source.Clone();

        int startY = Math.Max(0, cy - radius);
        int endY = Math.Min(height, cy + radius);
        int startX = Math.Max(0, cx - radius);
        int endX = Math.Min(width, cx + radius);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                if (dist >= radius || dist < 0.5f) continue;

                float normDist = dist / radius;
                // Parabolic falloff: strong in center, zero at edge
                float bulgeFactor = (1.0f - normDist * normDist) * strength;

                float srcX = x - dx * bulgeFactor;
                float srcY = y - dy * bulgeFactor;

                int sx = Math.Clamp((int)srcX, 0, width - 1);
                int sy = Math.Clamp((int)srcY, 0, height - 1);

                output[y * width + x] = source[sy * width + sx];
            }
        }
        image.Pixels = output;
    }

    private static void ApplyTwist(SKBitmap image, Random random)
    {
        int width = image.Width;
        int height = image.Height;

        // Random center biased toward the middle area
        int cx = width / 4 + random.Next(width / 2);
        int cy = height / 4 + random.Next(height / 2);

        // Radius: 15-30% of the smaller dimension
        int minDim = Math.Min(width, height);
        int radius = (int)(minDim * (0.15 + random.NextDouble() * 0.15));
        if (radius < 10) return;

        // Twist angle in radians: 0.8 to 2.0 (strong enough to be visible)
        float maxAngle = 0.8f + (float)(random.NextDouble() * 1.2);
        if (random.Next(2) == 0) maxAngle = -maxAngle; // random direction

        SKColor[] source = image.Pixels;
        SKColor[] output = (SKColor[])source.Clone();

        int startY = Math.Max(0, cy - radius);
        int endY = Math.Min(height, cy + radius);
        int startX = Math.Max(0, cx - radius);
        int endX = Math.Min(width, cx + radius);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                if (dist >= radius) continue;

                // Twist angle falls off from center to edge
                float normDist = dist / radius;
                float angle = maxAngle * (1.0f - normDist);

                float cos = MathF.Cos(angle);
                float sin = MathF.Sin(angle);

                // Rotate the pixel around the center
                float srcX = cx + dx * cos - dy * sin;
                float srcY = cy + dx * sin + dy * cos;

                int sx = Math.Clamp((int)srcX, 0, width - 1);
                int sy = Math.Clamp((int)srcY, 0, height - 1);

                output[y * width + x] = source[sy * width + sx];
            }
        }
        image.Pixels = output;
    }

}
