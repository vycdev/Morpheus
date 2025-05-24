using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class ImageDeepFryer
{
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

        Bitmap originalBitmap;
        try
        {
            using (MemoryStream ms = new MemoryStream(imageData))
            {
                originalBitmap = new Bitmap(ms);
            }
        }
        catch (Exception ex)
        {
            // Consider specific exception handling or re-throwing
            throw new ArgumentException("Invalid image data.", nameof(imageData), ex);
        }

        using (originalBitmap)
        {
            Bitmap friedBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, originalBitmap.PixelFormat);
            Random random = new Random();

            for (int y = 0; y < originalBitmap.Height; y++)
            {
                for (int x = 0; x < originalBitmap.Width; x++)
                {
                    Color originalColor = originalBitmap.GetPixel(x, y);

                    // 1. Apply Contrast
                    double rC = (originalColor.R / 255.0 - 0.5) * contrastFactor + 0.5;
                    double gC = (originalColor.G / 255.0 - 0.5) * contrastFactor + 0.5;
                    double bC = (originalColor.B / 255.0 - 0.5) * contrastFactor + 0.5;

                    // Convert back to 0-255 range and clamp
                    float rContrast = (float)(rC * 255.0);
                    float gContrast = (float)(gC * 255.0);
                    float bContrast = (float)(bC * 255.0);

                    // 2. Apply Saturation (simple method)
                    float L = 0.299f * rContrast + 0.587f * gContrast + 0.114f * bContrast; // Luminance
                    float rSat = L + saturationFactor * (rContrast - L);
                    float gSat = L + saturationFactor * (gContrast - L);
                    float bSat = L + saturationFactor * (bContrast - L);

                    // 3. Apply Noise
                    int rN = ClampToByte(rSat + random.Next(-noiseAmount, noiseAmount + 1));
                    int gN = ClampToByte(gSat + random.Next(-noiseAmount, noiseAmount + 1));
                    int bN = ClampToByte(bSat + random.Next(-noiseAmount, noiseAmount + 1));

                    friedBitmap.SetPixel(x, y, Color.FromArgb(originalColor.A, rN, gN, bN));
                }
            }

            using (MemoryStream outputMs = new MemoryStream())
            {
                // Try to preserve original format, with fallbacks
                ImageFormat outputFormat = originalBitmap.RawFormat;
                if (outputFormat.Guid.Equals(ImageFormat.MemoryBmp.Guid))
                {
                    // MemoryBmp is not a savable format, default to Png or Jpeg
                    outputFormat = ImageFormat.Png;
                }
                else if (IsPixelFormatIndexed(originalBitmap.PixelFormat) && !outputFormat.Equals(ImageFormat.Gif))
                {
                    // If original was indexed (like some PNGs) but not GIF, PNG is a good choice
                    // GIF manipulation often better saved as PNG if frames aren't handled
                    outputFormat = ImageFormat.Png;
                }
                else if (!outputFormat.Equals(ImageFormat.Jpeg) && !outputFormat.Equals(ImageFormat.Png) && !outputFormat.Equals(ImageFormat.Bmp) && !outputFormat.Equals(ImageFormat.Gif) && !outputFormat.Equals(ImageFormat.Tiff))
                {
                    // If it's an unknown or less common format, default to Jpeg for deep fry (lossy is often fine)
                    outputFormat = ImageFormat.Jpeg;
                }


                // For deep-fried effect, JPEG is often acceptable due to its lossy nature.
                // If transparency is critical, PNG would be better.
                // Here, we try to use original or a sensible default like JPEG.
                if (outputFormat.Equals(ImageFormat.Gif))
                {
                    // If it was a GIF, saving as PNG is usually better after pixel manipulation unless handling frames.
                    friedBitmap.Save(outputMs, ImageFormat.Png);
                }
                else if (outputFormat.Equals(ImageFormat.Jpeg))
                {
                    friedBitmap.Save(outputMs, ImageFormat.Jpeg);
                }
                else
                {
                    // For most other cases (PNG, BMP, TIFF that wasn't MemoryBMP)
                    // or if we defaulted, PNG is a good versatile choice.
                    friedBitmap.Save(outputMs, outputFormat);
                }
                return outputMs.ToArray();
            }
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

    private static bool IsPixelFormatIndexed(PixelFormat pixFormat)
    {
        // These are the indexed pixel formats
        return pixFormat == PixelFormat.Format1bppIndexed ||
               pixFormat == PixelFormat.Format4bppIndexed ||
               pixFormat == PixelFormat.Format8bppIndexed ||
               (pixFormat & PixelFormat.Indexed) == PixelFormat.Indexed; // General check
    }
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