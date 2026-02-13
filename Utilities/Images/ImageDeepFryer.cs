using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;

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

    private static readonly Color[] BrightColors =
    [
        Color.White, Color.Yellow, Color.Cyan, Color.Magenta, Color.Lime,
        Color.Gold, Color.GreenYellow, Color.Aqua, Color.Chartreuse,
        Color.FromRgb(255, 255, 100), Color.FromRgb(255, 100, 255),
        Color.FromRgb(100, 255, 255), Color.FromRgb(255, 200, 50),
        Color.FromRgb(200, 255, 50), Color.FromRgb(255, 150, 200)
    ];

    /// <summary>
    /// Applies a "deep-fried" effect to an image.
    /// </summary>
    /// <param name="imageData">The byte array representing the original image.</param>
    /// <param name="contrastFactor">Factor for increasing contrast (e.g., 2.0 to 4.0). Higher is more contrast.</param>
    /// <param name="saturationFactor">Factor for increasing saturation (e.g., 1.5 to 3.0). Higher is more saturated.</param>
    /// <param name="noiseAmount">Amount of noise to add (e.g., 20 to 50). Max 255.</param>
    /// <returns>A byte array representing the deep-fried image.</returns>
    public static byte[] DeepFry(byte[] imageData, double contrastFactor = 2.5, float saturationFactor = 2.0f, int noiseAmount = 30)
    {
        if (imageData == null || imageData.Length == 0)
        {
            throw new ArgumentNullException(nameof(imageData));
        }

        IImageFormat? originalFormat = null;
        try
        {
            using MemoryStream ms = new(imageData);
            // Detect format first (API changed between ImageSharp versions)
            ms.Position = 0;
            originalFormat = Image.DetectFormat(ms);
            ms.Position = 0;
            using Image<Rgba32> input = Image.Load<Rgba32>(ms);

            int width = input.Width;
            int height = input.Height;
            using Image<Rgba32> output = input.Clone();

            Random random = new();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Rgba32 orig = input[x, y];

                    // 1. Contrast
                    double rC = (orig.R / 255.0 - 0.5) * contrastFactor + 0.5;
                    double gC = (orig.G / 255.0 - 0.5) * contrastFactor + 0.5;
                    double bC = (orig.B / 255.0 - 0.5) * contrastFactor + 0.5;

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

                    output[x, y] = new Rgba32((byte)rN, (byte)gN, (byte)bN, orig.A);
                }
            }

            using MemoryStream outputMs = new();

            // Prefer to save as PNG for safety; if original is JPEG and doesn't have alpha, save as JPEG
            if (originalFormat != null && string.Equals(originalFormat.Name, "JPEG", StringComparison.OrdinalIgnoreCase))
            {
                output.SaveAsJpeg(outputMs);
            }
            else if (originalFormat != null && string.Equals(originalFormat.Name, "GIF", StringComparison.OrdinalIgnoreCase))
            {
                // animated GIFs are not handled; save as PNG
                output.SaveAsPng(outputMs);
            }
            else
            {
                output.SaveAsPng(outputMs);
            }

            return outputMs.ToArray();
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
        byte[] friedData = DeepFry(imageData, contrastFactor: 3.5, saturationFactor: 3.0f, noiseAmount: 45);

        IImageFormat? originalFormat = null;
        using MemoryStream ms = new(friedData);
        originalFormat = Image.DetectFormat(ms);
        ms.Position = 0;
        using Image<Rgba32> image = Image.Load<Rgba32>(ms);

        Random random = new();
        int width = image.Width;
        int height = image.Height;

        // 1. Overlays (before effects so they get distorted too)
        (Rectangle regionA, Rectangle regionB) = GetPlacementRegions(width, height, random);
        bool emojiFirst = random.Next(2) == 0;
        Rectangle emojiRegion = emojiFirst ? regionA : regionB;
        Rectangle textRegion = emojiFirst ? regionB : regionA;

        // Emoji overlay from pre-rendered Twemoji PNGs
        string[] emojiFiles = _emojiImages.Value;
        if (emojiFiles.Length > 0)
            TryOverlayEmojiImage(image, emojiFiles[random.Next(emojiFiles.Length)], random, emojiRegion);

        // Text overlay (phrases + text emojis)
        if (!SystemFonts.Families.Any())
            throw new InvalidOperationException("No system fonts available");
        FontFamily defaultFont = SystemFonts.Families.First();
        string text = ShitpostTexts[random.Next(ShitpostTexts.Length)];
        TryOverlayFittedText(image, text, defaultFont, random, textRegion);

        // 2. Random distortion: bulge or twist
        if (random.Next(2) == 0)
            ApplyBulge(image, random);
        else
            ApplyTwist(image, random);

        // 3. Vignette (darkened edges)
        image.Mutate(ctx => ctx.Vignette(width * 0.75f, height * 0.75f));

        using MemoryStream outputMs = new();
        if (originalFormat != null && string.Equals(originalFormat.Name, "JPEG", StringComparison.OrdinalIgnoreCase))
            image.SaveAsJpeg(outputMs);
        else
            image.SaveAsPng(outputMs);

        return outputMs.ToArray();
    }

    private static void TryOverlayEmojiImage(Image<Rgba32> target, string emojiPath, Random random, Rectangle region)
    {
        try
        {
            using Image<Rgba32> emojiImage = Image.Load<Rgba32>(emojiPath);

            // Scale to fit region (30-60% of region height)
            float targetSize = region.Height * (0.3f + (float)(random.NextDouble() * 0.3));
            float scale = targetSize / Math.Max(emojiImage.Width, emojiImage.Height);
            int newW = Math.Max(1, (int)(emojiImage.Width * scale));
            int newH = Math.Max(1, (int)(emojiImage.Height * scale));
            emojiImage.Mutate(ctx => ctx.Resize(newW, newH));

            // Boost brightness so emoji survives the deepfry darkening
            emojiImage.Mutate(ctx => ctx.Brightness(1.5f).Contrast(1.5f));

            // Random non-uniform stretching, clamped to region
            float stretchX = 0.6f + (float)(random.NextDouble() * 1.5);
            float stretchY = 0.6f + (float)(random.NextDouble() * 1.5);
            int finalW = Math.Clamp((int)(emojiImage.Width * stretchX), 1, region.Width);
            int finalH = Math.Clamp((int)(emojiImage.Height * stretchY), 1, region.Height);
            emojiImage.Mutate(ctx => ctx.Resize(finalW, finalH));

            // Mild rotation (-20 to +20 degrees)
            float angle = (float)(random.NextDouble() * 40 - 20);
            emojiImage.Mutate(ctx => ctx.Rotate(angle));

            // Position within the region
            int maxX = Math.Max(0, region.Right - emojiImage.Width);
            int maxY = Math.Max(0, region.Bottom - emojiImage.Height);
            int x = random.Next(region.X, Math.Max(region.X + 1, maxX + 1));
            int y = random.Next(region.Y, Math.Max(region.Y + 1, maxY + 1));

            float opacity = 0.9f + (float)(random.NextDouble() * 0.1);
            target.Mutate(ctx => ctx.DrawImage(emojiImage, new Point(x, y), opacity));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEEPFRY] Emoji image overlay failed for \"{emojiPath}\": {ex.Message}");
        }
    }

    private static void TryOverlayFittedText(Image<Rgba32> target, string text, FontFamily fontFamily, Random random, Rectangle region)
    {
        try
        {
            OverlayFittedText(target, text, fontFamily, random, region);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEEPFRY] Text overlay failed for \"{text}\": {ex.Message}");
        }
    }

    private static (Rectangle, Rectangle) GetPlacementRegions(int width, int height, Random random)
    {
        int style = random.Next(4);
        return style switch
        {
            // Top / bottom halves
            0 => (new Rectangle(0, 0, width, height / 2),
                  new Rectangle(0, height / 2, width, height / 2)),
            // Left / right halves
            1 => (new Rectangle(0, 0, width / 2, height),
                  new Rectangle(width / 2, 0, width / 2, height)),
            // Top-left corner / bottom-right corner
            2 => (new Rectangle(0, 0, (int)(width * 0.45), (int)(height * 0.45)),
                  new Rectangle((int)(width * 0.55), (int)(height * 0.55), (int)(width * 0.45), (int)(height * 0.45))),
            // Top-right corner / bottom-left corner
            _ => (new Rectangle((int)(width * 0.55), 0, (int)(width * 0.45), (int)(height * 0.45)),
                  new Rectangle(0, (int)(height * 0.55), (int)(width * 0.45), (int)(height * 0.45))),
        };
    }

    private static void ApplyBulge(Image<Rgba32> image, Random random)
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

        using Image<Rgba32> source = image.Clone();

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

                image[x, y] = source[sx, sy];
            }
        }
    }

    private static void ApplyTwist(Image<Rgba32> image, Random random)
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

        using Image<Rgba32> source = image.Clone();

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

                image[x, y] = source[sx, sy];
            }
        }
    }

    private static void OverlayFittedText(Image<Rgba32> target, string text, FontFamily fontFamily, Random random, Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0) return;

        // Font size: 30-60% of region height
        float targetHeight = region.Height * (0.3f + (float)(random.NextDouble() * 0.3));
        float fontSize = Math.Max(12, targetHeight);

        FontStyle style = random.Next(3) switch
        {
            0 => FontStyle.Bold,
            1 => FontStyle.Italic,
            _ => FontStyle.Regular
        };
        Font font = fontFamily.CreateFont(fontSize, style);

        Color color = BrightColors[random.Next(BrightColors.Length)];
        float outlineWidth = Math.Max(1, fontSize / 20);

        // Create a temp image to render text with outline
        int tempW = (int)(fontSize * Math.Max(text.Length, 1) * 0.75f) + 40;
        int tempH = (int)(fontSize * 1.8f) + 20;
        if (tempW <= 0 || tempH <= 0) return;

        using Image<Rgba32> textImage = new(tempW, tempH, Color.Transparent);
        textImage.Mutate(ctx => ctx.DrawText(
            text, font,
            Brushes.Solid(color),
            Pens.Solid(Color.Black, outlineWidth),
            new PointF(5, 5)));

        // Boost brightness so text/emoji survives the deepfry darkening
        textImage.Mutate(ctx => ctx.Brightness(1.5f).Contrast(1.5f));

        // Random non-uniform stretching, clamped to region
        float stretchX = 0.6f + (float)(random.NextDouble() * 1.5);
        float stretchY = 0.6f + (float)(random.NextDouble() * 1.5);
        int newW = Math.Clamp((int)(textImage.Width * stretchX), 1, region.Width);
        int newH = Math.Clamp((int)(textImage.Height * stretchY), 1, region.Height);
        textImage.Mutate(ctx => ctx.Resize(newW, newH));

        // Mild rotation (-20 to +20 degrees)
        float angle = (float)(random.NextDouble() * 40 - 20);
        textImage.Mutate(ctx => ctx.Rotate(angle));

        // Position within the region
        int maxX = Math.Max(0, region.Right - textImage.Width);
        int maxY = Math.Max(0, region.Bottom - textImage.Height);
        int x = random.Next(region.X, Math.Max(region.X + 1, maxX + 1));
        int y = random.Next(region.Y, Math.Max(region.Y + 1, maxY + 1));

        float opacity = 0.9f + (float)(random.NextDouble() * 0.1);
        target.Mutate(ctx => ctx.DrawImage(textImage, new Point(x, y), opacity));
    }

    // ImageSharp implementation does not need PixelFormat helpers
}

/*
// How to use it (example):
// Assumes you have 'inputImageBytes' as your byte[]
// and a way to save 'outputImageBytes' (e.g., File.WriteAllBytes)

// byte[] inputImageBytes = File.ReadAllBytes("input.jpg");
// byte[] friedImageBytes = ImageDeepFryer.DeepFry(inputImageBytes);
// File.WriteAllBytes("output_fried.jpg", friedImageBytes);

// byte[] friedWithOptionsBytes = ImageDeepFryer.DeepFry(inputImageBytes, 
//                                                       contrastFactor: 3.0, 
//                                                       saturationFactor: 2.5f, 
//                                                       noiseAmount: 40);
// File.WriteAllBytes("output_fried_custom.jpg", friedWithOptionsBytes);
*/
