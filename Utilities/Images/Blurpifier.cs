using SkiaSharp;

namespace Morpheus.Utilities.Images;

public static class Blurpifier
{
    /// <summary>
    /// Blurpify an image: reduce resolution (pixelate) then apply small random warps.
    /// Returns a PNG byte array.
    /// </summary>
    public static byte[] Blurpify(byte[] inputImage, int pixelScale = 4, int maxOffset = 6, int smoothPasses = 2, int seed = 0)
    {
        if (inputImage == null) throw new ArgumentNullException(nameof(inputImage));
        if (pixelScale < 1) pixelScale = 1;
        if (maxOffset < 0) maxOffset = 0;
        seed = seed == 0 ? Environment.TickCount : seed;

        using SKBitmap src = SkiaImages.Decode(inputImage);
        int w = src.Width, h = src.Height;
        using SKBitmap small = SkiaImages.Resize(src, Math.Max(1, w / pixelScale), Math.Max(1, h / pixelScale));
        using SKBitmap pixelated = SkiaImages.Resize(small, w, h, nearest: true);
        SKColor[] pixels = pixelated.Pixels;
        SKColor[] output = new SKColor[w * h];

        // Build displacement
        double[] offX = new double[w * h];
        double[] offY = new double[w * h];
        var rnd = new Random(seed);
        for (int i = 0; i < offX.Length; i++)
        {
            offX[i] = (rnd.NextDouble() * 2.0 - 1.0) * maxOffset;
            offY[i] = (rnd.NextDouble() * 2.0 - 1.0) * maxOffset;
        }

        // Smooth
        for (int pass = 0; pass < Math.Max(0, smoothPasses); pass++)
        {
            double[] nx = new double[offX.Length];
            double[] ny = new double[offY.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    double sumx = 0, sumy = 0; int cnt = 0;
                    for (int yy = Math.Max(0, y - 1); yy <= Math.Min(h - 1, y + 1); yy++)
                        for (int xx = Math.Max(0, x - 1); xx <= Math.Min(w - 1, x + 1); xx++)
                        {
                            int j = yy * w + xx;
                            sumx += offX[j]; sumy += offY[j]; cnt++;
                        }
                    nx[idx] = sumx / cnt; ny[idx] = sumy / cnt;
                }
            offX = nx; offY = ny;
        }

        using SKBitmap dest = new(w, h);

        // Sample function with bilinear interpolation
        SKColor Sample(double fx, double fy)
        {
            if (fx <= 0) fx = 0; if (fy <= 0) fy = 0; if (fx >= w - 1) fx = w - 1; if (fy >= h - 1) fy = h - 1;
            int x0 = (int)Math.Floor(fx);
            int y0 = (int)Math.Floor(fy);
            int x1 = Math.Min(w - 1, x0 + 1);
            int y1 = Math.Min(h - 1, y0 + 1);
            float dx = (float)(fx - x0);
            float dy = (float)(fy - y0);

            SKColor c00 = pixels[y0 * w + x0];
            SKColor c10 = pixels[y0 * w + x1];
            SKColor c01 = pixels[y1 * w + x0];
            SKColor c11 = pixels[y1 * w + x1];

            // Linear blends
            float r0 = c00.Red + (c10.Red - c00.Red) * dx;
            float g0 = c00.Green + (c10.Green - c00.Green) * dx;
            float b0 = c00.Blue + (c10.Blue - c00.Blue) * dx;
            float a0 = c00.Alpha + (c10.Alpha - c00.Alpha) * dx;

            float r1 = c01.Red + (c11.Red - c01.Red) * dx;
            float g1 = c01.Green + (c11.Green - c01.Green) * dx;
            float b1 = c01.Blue + (c11.Blue - c01.Blue) * dx;
            float a1 = c01.Alpha + (c11.Alpha - c01.Alpha) * dx;

            byte r = (byte)Math.Clamp(r0 + (r1 - r0) * dy, 0, 255);
            byte g = (byte)Math.Clamp(g0 + (g1 - g0) * dy, 0, 255);
            byte b = (byte)Math.Clamp(b0 + (b1 - b0) * dy, 0, 255);
            byte a = (byte)Math.Clamp(a0 + (a1 - a0) * dy, 0, 255);

            return new SKColor(r, g, b, a);
        }

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int idx = row + x;
                double sx = x + offX[idx];
                double sy = y + offY[idx];
                output[idx] = Sample(sx, sy);
            }
        }

        dest.Pixels = output;
        return SkiaImages.Encode(dest);
    }
}
