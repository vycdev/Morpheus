using System;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

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

        using var inStream = new MemoryStream(inputImage);
        using Image<Rgba32> src = Image.Load<Rgba32>(inStream);
        int w = src.Width, h = src.Height;

        // Pixelate: downscale then upscale
        int w2 = Math.Max(1, w / pixelScale);
        int h2 = Math.Max(1, h / pixelScale);

        using Image<Rgba32> small = src.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(w2, h2),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Bicubic
        }));

        using Image<Rgba32> pixelated = small.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(w, h),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.NearestNeighbor
        }));

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

        using Image<Rgba32> dest = new(w, h);

        // Sample function with bilinear interpolation
        Rgba32 Sample(double fx, double fy)
        {
            if (fx <= 0) fx = 0; if (fy <= 0) fy = 0; if (fx >= w - 1) fx = w - 1; if (fy >= h - 1) fy = h - 1;
            int x0 = (int)Math.Floor(fx);
            int y0 = (int)Math.Floor(fy);
            int x1 = Math.Min(w - 1, x0 + 1);
            int y1 = Math.Min(h - 1, y0 + 1);
            float dx = (float)(fx - x0);
            float dy = (float)(fy - y0);

            Rgba32 c00 = pixelated[x0, y0];
            Rgba32 c10 = pixelated[x1, y0];
            Rgba32 c01 = pixelated[x0, y1];
            Rgba32 c11 = pixelated[x1, y1];

            // Linear blends
            float r0 = c00.R + (c10.R - c00.R) * dx;
            float g0 = c00.G + (c10.G - c00.G) * dx;
            float b0 = c00.B + (c10.B - c00.B) * dx;
            float a0 = c00.A + (c10.A - c00.A) * dx;

            float r1 = c01.R + (c11.R - c01.R) * dx;
            float g1 = c01.G + (c11.G - c01.G) * dx;
            float b1 = c01.B + (c11.B - c01.B) * dx;
            float a1 = c01.A + (c11.A - c01.A) * dx;

            byte r = (byte)Math.Clamp(r0 + (r1 - r0) * dy, 0, 255);
            byte g = (byte)Math.Clamp(g0 + (g1 - g0) * dy, 0, 255);
            byte b = (byte)Math.Clamp(b0 + (b1 - b0) * dy, 0, 255);
            byte a = (byte)Math.Clamp(a0 + (a1 - a0) * dy, 0, 255);

            return new Rgba32(r, g, b, a);
        }

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int idx = row + x;
                double sx = x + offX[idx];
                double sy = y + offY[idx];
                dest[x, y] = Sample(sx, sy);
            }
        }

        using var outStream = new MemoryStream();
        dest.SaveAsPng(outStream);
        return outStream.ToArray();
    }
}
