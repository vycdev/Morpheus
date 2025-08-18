using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace Morpheus.Utilities.Images;

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
