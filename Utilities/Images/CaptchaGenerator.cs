using System;
using System.IO;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Linq;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Drawing;
using SixLabors.Fonts;

namespace Morpheus.Utilities.Images;

public class CaptchaResult(string captchaText, byte[] captchaImageBytes)
{
    public string CaptchaText { get; } = captchaText;
    public byte[] CaptchaImageBytes { get; } = captchaImageBytes;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

public class CaptchaGenerator
{
    private readonly int _width;
    private readonly int _height;
    private readonly int _textLength;
    private readonly Random _random = new();

    private readonly string _charSet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
    private readonly string[] _fontFamilies = new[] { "Arial", "Verdana", "Times New Roman", "Courier New", "Tahoma", "Georgia", "Comic Sans MS" };
    private readonly Color[] _backgroundColors = new[] { Color.WhiteSmoke, Color.LightGray, Color.FromRgb(240, 240, 240), Color.AliceBlue, Color.Lavender };

    private readonly FontCollection _fontCollection = new();
    private readonly FontFamily _defaultFamily;

    public CaptchaGenerator(int width = 230, int height = 70, int textLength = 8)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        if (textLength <= 0) throw new ArgumentOutOfRangeException(nameof(textLength), "Text length must be positive.");

        _width = width;
        _height = height;
        _textLength = textLength;

        // Register a default system font fallback; ImageSharp won't find system fonts by family name reliably in containers
        // We embed a fallback using the default fonts from the runtime if available
        _defaultFamily = SystemFonts.Families.Any() ? SystemFonts.Families.First() : throw new InvalidOperationException("No system fonts available");
    }

    public CaptchaResult GenerateCaptcha()
    {
        string captchaText = GenerateRandomText();

        using Image<Rgba32> image = new(_width, _height, _backgroundColors[_random.Next(_backgroundColors.Length)]);

        string drawn = DrawText(image, captchaText);
        AddDistortionAndNoise(image);

        using MemoryStream ms = new();
        image.SaveAsPng(ms);
        return new CaptchaResult(drawn, ms.ToArray());
    }

    private string GenerateRandomText()
    {
        StringBuilder sb = new(_textLength);
        for (int i = 0; i < _textLength; i++) sb.Append(_charSet[_random.Next(_charSet.Length)]);
        return sb.ToString();
    }

    private string DrawText(Image<Rgba32> image, string text)
    {
        float currentX = _random.Next(5, 10);
        StringBuilder drawn = new();

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            string familyName = _fontFamilies[_random.Next(_fontFamilies.Length)];
            FontFamily? family = SystemFonts.Families.FirstOrDefault(f => string.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase));
            FontFamily resolvedFamily = family ?? _defaultFamily;

            int fontSize = _random.Next(Math.Max(16, _height / 3), Math.Min(_height * 2 / 3, _height - 10));
            if (fontSize <= 0) fontSize = 16;

            FontStyle style = FontStyle.Regular;
            int styleRoll = _random.Next(10);
            if (styleRoll < 3) style = FontStyle.Bold;
            else if (styleRoll < 5) style = FontStyle.Italic;

            Font font = resolvedFamily.CreateFont(fontSize, style);

            // Approximate character measurement: width = size * 0.6, height = size
            float approxWidth = font.Size * 0.6f;
            float approxHeight = font.Size;

            if (currentX + approxWidth + 5 > _width) break;

            float maxTop = Math.Max(5f, _height - approxHeight - 5f);
            float top = (float)(_random.NextDouble() * (maxTop - 5f) + 5f);

            var rndCol = GetRandomDarkRgba();
            var textColor = Color.FromRgba(rndCol.R, rndCol.G, rndCol.B, rndCol.A);
            image.Mutate(ctx => ctx.DrawText(ch.ToString(), font, textColor, new PointF(currentX, top)));

            currentX += approxWidth * (0.9f + (float)_random.NextDouble() * 0.25f);
            currentX += _random.Next(1, Math.Max(2, _height / 20));

            drawn.Append(ch);
            if (currentX >= _width - 5) break;
        }

        return drawn.ToString();
    }

    private void AddDistortionAndNoise(Image<Rgba32> image)
    {
        // Draw bezier-like curves using shapes
        int numDistortionLines = _random.Next(2, 5);
        for (int i = 0; i < numDistortionLines; i++)
        {
            var p1 = new PointF(_random.Next(_width), _random.Next(_height));
            var p2 = new PointF(_random.Next(_width), _random.Next(_height));
            var ctrl1 = new PointF(_random.Next(_width), _random.Next(_height));
            var ctrl2 = new PointF(_random.Next(_width), _random.Next(_height));
            // Draw a bezier by sampling points and drawing small filled ellipses along the curve
            int steps = 24;
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float u = 1 - t;
                float bx = u * u * u * p1.X + 3 * u * u * t * ctrl1.X + 3 * u * t * t * ctrl2.X + t * t * t * p2.X;
                float by = u * u * u * p1.Y + 3 * u * u * t * ctrl1.Y + 3 * u * t * t * ctrl2.Y + t * t * t * p2.Y;
                var c = GetRandomDarkRgba(150);
                // Draw a small filled rectangle as a dot
                float size = Math.Max(1, _height / 120);
                image.Mutate(ctx => ctx.Fill(Color.FromRgba(c.R, c.G, c.B, c.A), new SixLabors.ImageSharp.Drawing.RectangularPolygon(bx - size / 2, by - size / 2, size, size)));
            }
        }

        // Straight noise lines
        int numNoiseLines = _random.Next(_width / 20, Math.Max(1, _width / 10));
        for (int i = 0; i < numNoiseLines; i++)
        {
            var p1 = new PointF(_random.Next(_width), _random.Next(_height));
            var p2 = new PointF(_random.Next(_width), _random.Next(_height));
            // Draw line by sampling points and filling small circles
            int segments = 12;
            for (int s = 0; s <= segments; s++)
            {
                float t = s / (float)segments;
                float lx = p1.X + (p2.X - p1.X) * t;
                float ly = p1.Y + (p2.Y - p1.Y) * t;
                var c = GetRandomGrayRgba(180);
                image.Mutate(ctx => ctx.Fill(Color.FromRgba(c.R, c.G, c.B, c.A), new SixLabors.ImageSharp.Drawing.RectangularPolygon(lx - 0.5f, ly - 0.5f, 1, 1)));
            }
        }

        // Pixel noise
        int numNoiseDots = (int)(_width * _height * (_random.NextDouble() * 0.005 + 0.005));
        for (int i = 0; i < numNoiseDots; i++)
        {
            int x = _random.Next(_width);
            int y = _random.Next(_height);
            var col = GetRandomGrayRgba(200);
            image[x, y] = col;
        }
    }

    private Rgba32 GetRandomDarkRgba(int max = 120)
    {
        return new Rgba32((byte)_random.Next(0, max), (byte)_random.Next(0, max), (byte)_random.Next(0, max));
    }

    private Rgba32 GetRandomGrayRgba(int max = 255)
    {
        byte v = (byte)_random.Next(0, max + 1);
        return new Rgba32(v, v, v);
    }
}

/*
// How to use it (example):
// Ensure you have System.Drawing.Common NuGet package if on .NET Core/5+
// For Linux/macOS, libgdiplus might be needed for System.Drawing.

// var captchaGenerator = new CaptchaGenerator(width: 220, height: 80, textLength: 6);
// CaptchaResult result = captchaGenerator.GenerateCaptcha();

// // IMPORTANT: Use result.CaptchaText for validation, as it contains only the characters actually drawn.
// string captchaTextForValidation = result.CaptchaText; 
// byte[] imageBytes = result.CaptchaImageBytes; 

// // Example: Save to a file
// System.IO.File.WriteAllBytes("captcha.png", imageBytes);
// Console.WriteLine($"CAPTCHA Text (Drawn): {captchaTextForValidation} (Timestamp: {result.Timestamp})");

// // If you need to ensure all _textLength characters always appear, 
// // you may need to increase width, decrease textLength, or reduce font size variation.
*/
