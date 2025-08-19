using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Drawing;
using SixLabors.Fonts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Morpheus.Utilities.Images;

// Simple activity graph generator: renders a small line chart for multiple series.
public static class ActivityGraphGenerator
{
    // Returns PNG bytes. series: dictionary of "label" -> list of daily values (oldest -> newest)
    public static byte[] GenerateLineChart(Dictionary<string, List<int>> series, int days, int width = 1000, int height = 400, DateTime? start = null)
    {
        if (series == null || series.Count == 0) throw new ArgumentException("No series provided.");
        if (days <= 0) throw new ArgumentOutOfRangeException(nameof(days));

        int marginLeft = 80;
        int marginRight = 160;
        int marginTop = 40;
        int marginBottom = 60;

        using Image<Rgba32> image = new Image<Rgba32>(width, height, Color.White);

        // drawing area
        int plotWidth = width - marginLeft - marginRight;
        int plotHeight = height - marginTop - marginBottom;
        int originX = marginLeft;
        int originY = marginTop + plotHeight; // bottom-left of plotting area

        // Flatten values to find max
        int globalMax = 1;
        foreach (var kv in series)
        {
            int max = kv.Value.DefaultIfEmpty(0).Max();
            if (max > globalMax) globalMax = max;
        }

        // Padding top
        globalMax = (int)Math.Ceiling(globalMax * 1.1);
        if (globalMax <= 0) globalMax = 1;

        // grid lines and labels
        int yTicks = 5;
        Font font = GetFont(12);

        image.Mutate(ctx =>
        {
            // background
            ctx.Fill(Color.White);

            // draw axes (simple thin rectangles)
            ctx.Fill(Color.LightGray, new RectangleF(originX - 1, marginTop, 1, plotHeight));
            ctx.Fill(Color.LightGray, new RectangleF(originX, originY - 1, plotWidth, 1));

            // horizontal grid and y labels
            for (int i = 0; i <= yTicks; i++)
            {
                float t = i / (float)yTicks;
                float y = marginTop + t * plotHeight;
                ctx.Fill(Color.LightGray, new RectangleF(originX, y - 0.5f, plotWidth, 1));

                int labelVal = (int)Math.Round((1 - t) * globalMax);
                string lbl = labelVal.ToString();
                // approximate text size
                var approxWidth = lbl.Length * font.Size * 0.6f;
                var approxHeight = font.Size;
                ctx.DrawText(lbl, font, Color.Black, new PointF(originX - 10 - approxWidth, y - approxHeight / 2));
            }

            // x labels: show day ticks evenly
            DateTime baseStart = (start ?? DateTime.UtcNow.Date.AddDays(-(days - 1))).Date;
            for (int d = 0; d < days; d += Math.Max(1, days / 8))
            {
                float x = originX + (d / (float)(days - 1)) * plotWidth;
                DateTime dt = baseStart.AddDays(d);
                string lbl = dt.ToString("MM-dd");
                var approxWidth = lbl.Length * font.Size * 0.6f;
                ctx.DrawText(lbl, font, Color.Black, new PointF(x - approxWidth / 2, originY + 6));
            }
        });

        // color palette (distinct colors)
        List<Color> palette = new()
        {
            Color.ParseHex("#1f77b4"),
            Color.ParseHex("#ff7f0e"),
            Color.ParseHex("#2ca02c"),
            Color.ParseHex("#d62728"),
            Color.ParseHex("#9467bd"),
            Color.ParseHex("#8c564b"),
            Color.ParseHex("#e377c2"),
            Color.ParseHex("#7f7f7f"),
            Color.ParseHex("#bcbd22"),
            Color.ParseHex("#17becf")
        };

        int idx = 0;
        foreach (var kv in series)
        {
            var label = kv.Key;
            var values = kv.Value;
            // pad or trim to days
            List<int> data = new List<int>(new int[days]);
            for (int i = 0; i < days; i++)
            {
                if (i < values.Count) data[i] = values[i];
                else data[i] = 0;
            }

            // convert to points
            PointF[] points = new PointF[days];
            for (int i = 0; i < days; i++)
            {
                float x = originX + (i / (float)(days - 1)) * plotWidth;
                float y = marginTop + (1 - (data[i] / (float)globalMax)) * plotHeight;
                points[i] = new PointF(x, y);
            }

            var color = palette[idx % palette.Count];
            // draw points and connecting segments by sampling (avoid DrawLines API differences)
            image.Mutate(ctx =>
            {
                for (int i = 0; i < points.Length; i++)
                {
                    var p = points[i];
                    ctx.Fill(color, new EllipsePolygon(p.X, p.Y, 3));
                    if (i > 0)
                    {
                        var p0 = points[i - 1];
                        var p1 = p;
                        float dx = p1.X - p0.X;
                        float dy = p1.Y - p0.Y;
                        float dist = MathF.Sqrt(dx * dx + dy * dy);
                        int steps = Math.Max(1, (int)(dist / 2));
                        for (int s = 0; s <= steps; s++)
                        {
                            float t = s / (float)steps;
                            float sx = p0.X + dx * t;
                            float sy = p0.Y + dy * t;
                            ctx.Fill(color, new EllipsePolygon(sx, sy, 1.5f));
                        }
                    }
                }
            });

            // legend
            float legendX = width - marginRight + 10;
            float legendY = marginTop + idx * 22;
            var legendFont = font;
            image.Mutate(ctx =>
            {
                ctx.Fill(color, new RectangleF(legendX, legendY + 4, 12, 12));
                ctx.DrawText(label, legendFont, Color.Black, new PointF(legendX + 18, legendY));
            });

            idx++;
            if (idx >= 10) break; // top 10
        }

        // footer
        image.Mutate(ctx =>
        {
            Font f = GetFont(10);
            ctx.DrawText($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC", f, Color.Gray, new PointF(8, height - marginBottom + 20));
        });

        using MemoryStream ms = new();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static Font GetFont(float size)
    {
        // Prefer system fonts if available
        try
        {
            if (SystemFonts.Families != null && SystemFonts.Families.Any())
            {
                return SystemFonts.Families.First().CreateFont(size);
            }
        }
        catch { /* fallthrough */ }

        // Try common Linux font paths
        string[] candidates = new[] {
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/freefont/FreeSans.ttf",
            "/usr/share/fonts/truetype/msttcorefonts/Arial.ttf"
        };

        foreach (var path in candidates)
        {
            try
            {
                if (File.Exists(path))
                {
                    var fc = new FontCollection();
                    var family = fc.Add(path);
                    return family.CreateFont(size);
                }
            }
            catch { }
        }

        // Try scanning the usual truetype folder for any .ttf
        try
        {
            string root = "/usr/share/fonts/truetype";
            if (Directory.Exists(root))
            {
                var first = Directory.EnumerateFiles(root, "*.ttf", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(first))
                {
                    var fc = new FontCollection();
                    var family = fc.Add(first);
                    return family.CreateFont(size);
                }
            }
        }
        catch { }

        // If nothing found, provide a helpful exception so caller can log and skip rendering
        throw new InvalidOperationException("No usable font found on the system. Please install a TTF font such as DejaVuSans in the container (e.g. package 'fonts-dejavu').");
    }
}
