using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace Morpheus.Utilities.Images;

public static class ActivityGraphGenerator
{
    // Returns PNG bytes. Values run from oldest to newest; missing days are zero-filled.
    public static byte[] GenerateLineChart(Dictionary<string, List<int>> series, int days, int width = 1000, int height = 520, DateTime? start = null)
    {
        if (series == null || series.Count == 0) throw new ArgumentException("No series provided.");
        if (days <= 0) throw new ArgumentOutOfRangeException(nameof(days));
        const int marginLeft = 80, marginRight = 160, marginTop = 40, marginBottom = 100;
        if (width <= marginLeft + marginRight) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= marginTop + marginBottom) throw new ArgumentOutOfRangeException(nameof(height));

        using SKBitmap image = new(width, height);
        using SKCanvas canvas = new(image);
        canvas.Clear(SKColors.White);
        using SKFont font = SkiaImages.CreateFont(12);
        using SKShaper shaper = new(font.Typeface);
        using SKPaint ink = new() { Color = SKColors.Black, IsAntialias = true };
        using SKPaint grid = new() { Color = SKColors.LightGray, StrokeWidth = 1 };
        int plotWidth = width - marginLeft - marginRight;
        int plotHeight = height - marginTop - marginBottom;
        int originY = marginTop + plotHeight;
        const int yTicks = 6;
        int maxValue = Math.Max(1, series.Values.SelectMany(v => v).DefaultIfEmpty(0).Max());
        double niceStep = NiceStep(Math.Ceiling(maxValue * 1.1) / yTicks);
        double globalMax = Math.Max(1, niceStep * yTicks);

        canvas.DrawLine(marginLeft, marginTop, marginLeft, originY, grid);
        for (int i = 0; i <= yTicks; i++)
        {
            float y = marginTop + i / (float)yTicks * plotHeight;
            canvas.DrawLine(marginLeft, y, marginLeft + plotWidth, y, grid);
            string label = Math.Round((yTicks - i) * niceStep).ToString();
            canvas.DrawText(label, marginLeft - 10, y - (font.Metrics.Ascent + font.Metrics.Descent) / 2,
                SKTextAlign.Right, font, ink);
        }

        DateTime baseStart = (start ?? DateTime.UtcNow.Date.AddDays(-(days - 1))).Date;
        int denominator = Math.Max(1, days - 1);
        for (int day = 0; day < days; day += Math.Max(1, days / 8))
        {
            float x = marginLeft + day / (float)denominator * plotWidth;
            canvas.Save();
            canvas.RotateDegrees(-0.6f * 180 / MathF.PI, x, originY + 45);
            canvas.DrawText(baseStart.AddDays(day).ToString("yyyy-MM-dd"), x, originY + 45 - font.Metrics.Ascent, SKTextAlign.Left, font, ink);
            canvas.Restore();
        }

        SKColor[] palette = [
            SKColor.Parse("#1f77b4"), SKColor.Parse("#ff7f0e"), SKColor.Parse("#2ca02c"),
            SKColor.Parse("#d62728"), SKColor.Parse("#9467bd"), SKColor.Parse("#8c564b"),
            SKColor.Parse("#e377c2"), SKColor.Parse("#7f7f7f"), SKColor.Parse("#bcbd22"), SKColor.Parse("#17becf")
        ];
        int index = 0;
        foreach (var (label, values) in series.Take(10))
        {
            using SKPaint plot = new() { Color = palette[index], IsAntialias = true, StrokeWidth = 3 };
            SKPoint previous = default;
            for (int day = 0; day < days; day++)
            {
                int value = day < values.Count ? values[day] : 0;
                SKPoint point = new(marginLeft + day / (float)denominator * plotWidth,
                    marginTop + (float)(1 - value / globalMax) * plotHeight);
                if (day > 0) canvas.DrawLine(previous, point, plot);
                canvas.DrawCircle(point, 3, plot);
                previous = point;
            }
            float legendX = width - marginRight + 10;
            float legendY = marginTop + index * 22;
            canvas.DrawRect(legendX, legendY + 4, 12, 12, plot);
            canvas.DrawShapedText(shaper, label, legendX + 18, legendY - font.Metrics.Ascent, SKTextAlign.Left, font, ink);
            index++;
        }
        using SKFont footer = SkiaImages.CreateFont(10);
        ink.Color = SKColors.Gray;
        canvas.DrawText($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC", 8, height - 30 - footer.Metrics.Ascent, SKTextAlign.Left, footer, ink);
        return SkiaImages.Encode(image);
    }

    private static double NiceStep(double step)
    {
        if (step <= 0) return 1;
        double exp = Math.Floor(Math.Log10(step));
        double frac = step / Math.Pow(10, exp);
        double niceFrac;
        if (frac <= 1) niceFrac = 1;
        else if (frac <= 2) niceFrac = 2;
        else if (frac <= 5) niceFrac = 5;
        else niceFrac = 10;
        return niceFrac * Math.Pow(10, exp);
    }
}
