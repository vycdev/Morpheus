using System.Text;
using SkiaSharp;

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
    private readonly SKColor[] _backgroundColors = new[] { SKColors.WhiteSmoke, SKColors.LightGray, new SKColor(240, 240, 240), SKColors.AliceBlue, SKColors.Lavender };

    public CaptchaGenerator(int width = 230, int height = 70, int textLength = 8)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        if (textLength <= 0) throw new ArgumentOutOfRangeException(nameof(textLength), "Text length must be positive.");

        _width = width;
        _height = height;
        _textLength = textLength;
    }

    public CaptchaResult GenerateCaptcha()
    {
        string captchaText = GenerateRandomText();

        using SKBitmap image = new(_width, _height);
        using SKCanvas canvas = new(image);
        canvas.Clear(_backgroundColors[_random.Next(_backgroundColors.Length)]);

        string drawn = DrawText(canvas, captchaText);
        AddDistortionAndNoise(image, canvas);

        return new CaptchaResult(drawn, SkiaImages.Encode(image));
    }

    private string GenerateRandomText()
    {
        StringBuilder sb = new(_textLength);
        for (int i = 0; i < _textLength; i++) sb.Append(_charSet[_random.Next(_charSet.Length)]);
        return sb.ToString();
    }

    private string DrawText(SKCanvas canvas, string text)
    {
        float currentX = _random.Next(5, 10);
        StringBuilder drawn = new();

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            string familyName = _fontFamilies[_random.Next(_fontFamilies.Length)];
            int minSize = Math.Max(1, Math.Min(16, _height / 3));
            int maxSize = Math.Max(minSize + 1, Math.Min(_height * 2 / 3, _height - 10));
            int fontSize = _random.Next(minSize, maxSize);
            int styleRoll = _random.Next(10);
            SKFontStyle style = styleRoll < 3 ? SKFontStyle.Bold : styleRoll < 5 ? SKFontStyle.Italic : SKFontStyle.Normal;
            using SKFont font = SkiaImages.CreateFont(fontSize, style, familyName);

            // Measure glyphs so validation includes only the characters actually drawn.
            float approxWidth = font.MeasureText(ch.ToString());
            float approxHeight = font.Metrics.Descent - font.Metrics.Ascent;

            if (currentX + approxWidth + 5 > _width) break;

            float maxTop = Math.Max(5f, _height - approxHeight - 5f);
            float top = (float)(_random.NextDouble() * (maxTop - 5f) + 5f);

            using SKPaint paint = new() { Color = GetRandomDarkRgba(), IsAntialias = true };
            canvas.DrawText(ch.ToString(), currentX, top - font.Metrics.Ascent, SKTextAlign.Left, font, paint);

            currentX += approxWidth * (0.9f + (float)_random.NextDouble() * 0.25f);
            currentX += _random.Next(1, Math.Max(2, _height / 20));

            drawn.Append(ch);
            if (currentX >= _width - 5) break;
        }

        return drawn.ToString();
    }

    private void AddDistortionAndNoise(SKBitmap image, SKCanvas canvas)
    {
        // Draw bezier-like curves using shapes
        int numDistortionLines = _random.Next(2, 5);
        for (int i = 0; i < numDistortionLines; i++)
        {
            var p1 = new SKPoint(_random.Next(_width), _random.Next(_height));
            var p2 = new SKPoint(_random.Next(_width), _random.Next(_height));
            var ctrl1 = new SKPoint(_random.Next(_width), _random.Next(_height));
            var ctrl2 = new SKPoint(_random.Next(_width), _random.Next(_height));
            using SKPathBuilder builder = new();
            builder.MoveTo(p1);
            builder.CubicTo(ctrl1, ctrl2, p2);
            using SKPath path = builder.Detach();
            using SKPaint paint = new() { Color = GetRandomDarkRgba(150), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1, _height / 120f) };
            canvas.DrawPath(path, paint);
        }

        // Straight noise lines
        int numNoiseLines = _random.Next(_width / 20, Math.Max(1, _width / 10));
        for (int i = 0; i < numNoiseLines; i++)
        {
            var p1 = new SKPoint(_random.Next(_width), _random.Next(_height));
            var p2 = new SKPoint(_random.Next(_width), _random.Next(_height));
            using SKPaint paint = new() { Color = GetRandomGrayRgba(180), IsAntialias = true, StrokeWidth = 1 };
            canvas.DrawLine(p1, p2, paint);
        }

        // Pixel noise
        int numNoiseDots = (int)(_width * _height * (_random.NextDouble() * 0.005 + 0.005));
        for (int i = 0; i < numNoiseDots; i++)
        {
            int x = _random.Next(_width);
            int y = _random.Next(_height);
            var col = GetRandomGrayRgba(200);
            image.SetPixel(x, y, col);
        }
    }

    private SKColor GetRandomDarkRgba(int max = 120)
    {
        return new SKColor((byte)_random.Next(0, max), (byte)_random.Next(0, max), (byte)_random.Next(0, max));
    }

    private SKColor GetRandomGrayRgba(int max = 255)
    {
        byte v = (byte)_random.Next(0, max + 1);
        return new SKColor(v, v, v);
    }
}
