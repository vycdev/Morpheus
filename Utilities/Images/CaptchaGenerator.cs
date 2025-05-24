using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

public class CaptchaResult
{
    public string CaptchaText { get; }
    public byte[] CaptchaImageBytes { get; }
    public DateTime Timestamp { get; }

    public CaptchaResult(string captchaText, byte[] captchaImageBytes)
    {
        CaptchaText = captchaText;
        CaptchaImageBytes = captchaImageBytes;
        Timestamp = DateTime.UtcNow;
    }
}

public class CaptchaGenerator
{
    private readonly int _width;
    private readonly int _height;
    private readonly int _textLength;
    private readonly Random _random = new Random();

    private readonly string _charSet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
    private readonly string[] _fontFamilies = { "Arial", "Verdana", "Times New Roman", "Courier New", "Tahoma", "Georgia", "Comic Sans MS" };
    private readonly Color[] _backgroundColors = { Color.WhiteSmoke, Color.LightGray, Color.FromArgb(240, 240, 240), Color.AliceBlue, Color.Lavender };

    public CaptchaGenerator(int width = 400, int height = 70, int textLength = 8)
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
        string actualDrawnText = ""; // To store only the characters that were actually drawn

        using (Bitmap bitmap = new Bitmap(_width, _height, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias; // Better text
            graphics.Clear(_backgroundColors[_random.Next(_backgroundColors.Length)]);

            actualDrawnText = DrawCaptchaTextAndReturnDrawn(graphics, captchaText);
            AddDistortionAndNoise(graphics, bitmap);

            using (MemoryStream ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                // Return the text that was actually drawn, in case it's less than _textLength
                return new CaptchaResult(actualDrawnText, ms.ToArray());
            }
        }
    }

    private string GenerateRandomText()
    {
        StringBuilder sb = new StringBuilder(_textLength);
        for (int i = 0; i < _textLength; i++)
        {
            sb.Append(_charSet[_random.Next(_charSet.Length)]);
        }
        return sb.ToString();
    }

    private string DrawCaptchaTextAndReturnDrawn(Graphics graphics, string text)
    {
        float currentX = _random.Next(5, 10); // Initial X padding
        StringBuilder drawnTextBuilder = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            char characterToDraw = text[i];
            string fontFamily = _fontFamilies[_random.Next(_fontFamilies.Length)];
            // Ensure font size is not excessively large for the height
            int fontSize = _random.Next(Math.Max(16, _height / 3), Math.Min(_height * 2 / 3, _height - 10));
            if (fontSize <= 0) fontSize = 16; // Safety for very small heights

            FontStyle fontStyle = FontStyle.Regular;
            int styleRoll = _random.Next(10);
            if (styleRoll < 3) fontStyle = FontStyle.Bold;
            else if (styleRoll < 5) fontStyle = FontStyle.Italic;
            else if (styleRoll < 6 && (fontStyle & FontStyle.Bold) == 0) fontStyle |= FontStyle.Italic; // BoldItalic less frequent

            Font chosenFont = null;
            SizeF charSize = SizeF.Empty;

            try
            {
                chosenFont = new Font(fontFamily, fontSize, fontStyle);
                charSize = graphics.MeasureString(characterToDraw.ToString(), chosenFont);
            }
            catch (ArgumentException) // Font creation failed (e.g., not found, invalid style combination)
            {
                chosenFont?.Dispose();
                // Fallback font
                int fallbackFontSize = Math.Max(16, Math.Min(fontSize, _height * 3 / 5));
                chosenFont = new Font("Arial", fallbackFontSize, FontStyle.Bold); // Arial Bold is widely available
                charSize = graphics.MeasureString(characterToDraw.ToString(), chosenFont);
            }

            using (chosenFont) // Manages dispose for original or fallback font
            using (Brush brush = new SolidBrush(GetRandomDarkColor()))
            {
                // Check if the current character (with its measured size) can fit
                // Add a small margin (e.g., 5px) at the right edge of the image
                if (currentX + charSize.Width + 5 > _width)
                {
                    break; // Not enough space for this character, so stop.
                }

                // Vertical placement
                float maxCharTopY = Math.Max(5f, _height - charSize.Height - 5f); // Max Y for top of char
                float charTopY = (float)_random.NextDouble() * (maxCharTopY - 5f) + 5f; // Random Y for top of char
                if (charTopY < 0) charTopY = 0; // Safety

                // Center of the character for rotation
                float charCenterX = currentX + charSize.Width / 2;
                float charCenterY = charTopY + charSize.Height / 2;

                GraphicsState state = graphics.Save();
                graphics.TranslateTransform(charCenterX, charCenterY);

                float angle = _random.Next(-20, 21); // Reduced max angle slightly
                graphics.RotateTransform(angle);

                // Draw string centered at the (now transformed) origin (0,0)
                // Add slight vertical jitter relative to character's own height to make it less uniform
                float verticalJitter = (float)(_random.NextDouble() * 0.2 - 0.1) * charSize.Height; // +/- 10% of char height
                graphics.DrawString(characterToDraw.ToString(), chosenFont, brush, -charSize.Width / 2, -charSize.Height / 2 + verticalJitter);

                graphics.Restore(state);

                // Advance currentX for the next character
                currentX += charSize.Width * (0.9f + (float)_random.NextDouble() * 0.25f); // Range 0.9 to 1.15
                currentX += _random.Next(1, Math.Max(2, _height / 20)); // Add a small absolute random gap

                drawnTextBuilder.Append(characterToDraw);
            }

            if (currentX >= _width - 5) // Check if we've essentially run out of horizontal space
            {
                break;
            }
        }
        return drawnTextBuilder.ToString();
    }

    private void AddDistortionAndNoise(Graphics graphics, Bitmap bitmap)
    {
        // Add curved lines (distortions)
        int numDistortionLines = _random.Next(2, 5);
        for (int i = 0; i < numDistortionLines; i++)
        {
            Point p1 = new Point(_random.Next(_width), _random.Next(_height));
            Point p2 = new Point(_random.Next(_width), _random.Next(_height));
            Point ctrl1 = new Point(_random.Next(_width), _random.Next(_height));
            Point ctrl2 = new Point(_random.Next(_width), _random.Next(_height));
            int alpha = _random.Next(70, 130);
            using (Pen pen = new Pen(Color.FromArgb(alpha, GetRandomDarkColor(150)), Math.Max(1, _height / 35)))
            {
                try { graphics.DrawBezier(pen, p1, ctrl1, ctrl2, p2); }
                catch { /* GDI+ can sometimes throw generic errors with complex paths */ }
            }
        }

        // Add straight noise lines
        int numNoiseLines = _random.Next(_width / 20, _width / 10); // More lines based on width
        for (int i = 0; i < numNoiseLines; i++)
        {
            int x1 = _random.Next(_width);
            int y1 = _random.Next(_height);
            int x2 = _random.Next(_width);
            int y2 = _random.Next(_height);
            int alpha = _random.Next(40, 100);
            using (Pen pen = new Pen(Color.FromArgb(alpha, GetRandomGrayColorValue(180), GetRandomGrayColorValue(180), GetRandomGrayColorValue(180)), 1))
            {
                try { graphics.DrawLine(pen, x1, y1, x2, y2); } catch { }
            }
        }

        // Add pixel noise ("salt and pepper")
        int numNoiseDots = (int)(_width * _height * (_random.NextDouble() * 0.005 + 0.005)); // 0.5% to 1% pixel noise
        for (int i = 0; i < numNoiseDots; i++)
        {
            int x = _random.Next(_width);
            int y = _random.Next(_height);
            int alpha = _random.Next(70, 180);
            try { bitmap.SetPixel(x, y, Color.FromArgb(alpha, GetRandomGrayColorValue(200), GetRandomGrayColorValue(200), GetRandomGrayColorValue(200))); } catch { }
        }
    }

    private Color GetRandomDarkColor(int maxComponentValue = 120)
    {
        return Color.FromArgb(
            _random.Next(0, maxComponentValue),
            _random.Next(0, maxComponentValue),
            _random.Next(0, maxComponentValue)
        );
    }

    private int GetRandomGrayColorValue(int max = 255)
    {
        return _random.Next(0, max + 1);
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