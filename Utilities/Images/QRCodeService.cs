using QRCoder;
using System;
using System.Drawing; // Still needed by QRCoder for some internal operations or if you want Bitmap output
using System.IO;

public class QrCodeService
{
    /// <summary>
    /// Generates a QR code image as a byte array.
    /// </summary>
    /// <param name="data">The string data to encode in the QR code.</param>
    /// <param name="pixelsPerModule">The size of each "dot" (module) in the QR code. Higher means larger image.</param>
    /// <param name="eccLevel">The error correction level. Higher levels allow more of the QR code to be damaged/obscured and still be readable.</param>
    /// <returns>A byte array representing the QR code image in PNG format.</returns>
    public byte[] GenerateQrCode(string data, int pixelsPerModule = 20, QRCodeGenerator.ECCLevel eccLevel = QRCodeGenerator.ECCLevel.Q)
    {
        if (string.IsNullOrEmpty(data))
        {
            throw new ArgumentNullException(nameof(data), "Data to encode cannot be null or empty.");
        }
        if (pixelsPerModule <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelsPerModule), "Pixels per module must be greater than zero.");
        }

        byte[] qrCodeBytes;

        using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
        using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, eccLevel))
        // PngByteQRCode is convenient as it directly outputs byte[] for a PNG
        using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
        {
            qrCodeBytes = qrCode.GetGraphic(pixelsPerModule);
        }

        return qrCodeBytes;
    }

    /// <summary>
    /// Generates a QR code with custom colors.
    /// </summary>
    /// <param name="data">The string data to encode.</param>
    /// <param name="darkColorHtml">HTML color code for dark modules (e.g., "#000000" for black).</param>
    /// <param name="lightColorHtml">HTML color code for light modules (e.g., "#FFFFFF" for white).</param>
    /// <param name="pixelsPerModule">The size of each module.</param>
    /// <param name="eccLevel">Error correction level.</param>
    /// <param name="drawQuietZones">Whether to include the white border (quiet zone) around the QR code.</param>
    /// <returns>Byte array of the PNG QR code image.</returns>
    public byte[] GenerateQrCodeWithColors(
        string data,
        string darkColorHtml = "#000000",
        string lightColorHtml = "#FFFFFF",
        int pixelsPerModule = 20,
        QRCodeGenerator.ECCLevel eccLevel = QRCodeGenerator.ECCLevel.Q,
        bool drawQuietZones = true)
    {
        if (string.IsNullOrEmpty(data))
        {
            throw new ArgumentNullException(nameof(data), "Data to encode cannot be null or empty.");
        }
        if (pixelsPerModule <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelsPerModule), "Pixels per module must be greater than zero.");
        }

        Color darkColor = ColorTranslator.FromHtml(darkColorHtml);
        Color lightColor = ColorTranslator.FromHtml(lightColorHtml);

        byte[] qrCodeBytes;

        using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
        using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, eccLevel))
        using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
        {
            qrCodeBytes = qrCode.GetGraphic(pixelsPerModule, darkColor, lightColor, drawQuietZones);
        }
        return qrCodeBytes;
    }
}

/*
// How to use it:

public class ExampleUsage
{
    public static void Main(string[] args)
    {
        QrCodeService qrService = new QrCodeService();
        string dataToEncode = "https://www.example.com";

        // Generate a standard black and white QR code
        try
        {
            byte[] qrCodeBytes = qrService.GenerateQrCode(dataToEncode, pixelsPerModule: 15);
            File.WriteAllBytes("qrcode_standard.png", qrCodeBytes);
            Console.WriteLine("Standard QR code generated as qrcode_standard.png");

            // Generate a QR code with custom colors (e.g., dark blue on light yellow)
            byte[] qrCodeColorBytes = qrService.GenerateQrCodeWithColors(
                dataToEncode,
                darkColorHtml: "#000080", // Navy
                lightColorHtml: "#FFFFE0", // LightYellow
                pixelsPerModule: 20);
            File.WriteAllBytes("qrcode_color.png", qrCodeColorBytes);
            Console.WriteLine("Color QR code generated as qrcode_color.png");

            // Example with logo (more advanced, QRCoder supports this too)
            // This requires a Bitmap for the logo.
            // Note: QRCoder's built-in logo support renders it to a Bitmap first, then you'd convert to bytes.
            // PngByteQRCode doesn't have direct logo support, you'd use QRCode class then GetGraphic()
            // and convert the Bitmap to byte array.

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(dataToEncode, QRCodeGenerator.ECCLevel.H)) // Higher ECC for logo
            using (QRCode qrCodeWithLogo = new QRCode(qrCodeData))
            {
                // Assuming you have a logo.png file
                // Bitmap logo = null;
                // if (File.Exists("logo.png"))
                // {
                //    logo = new Bitmap("logo.png");
                // }

                // For simplicity, we'll skip the actual logo loading here.
                // If you had a logo Bitmap:
                // Bitmap qrCodeImageWithLogo = qrCodeWithLogo.GetGraphic(10, Color.Black, Color.White, logo);

                // Without logo, but using the QRCode class to show how to get Bitmap then bytes:
                Bitmap qrBitmap = qrCodeWithLogo.GetGraphic(10);
                using (MemoryStream ms = new MemoryStream())
                {
                    qrBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] qrBitmapBytes = ms.ToArray();
                    File.WriteAllBytes("qrcode_from_bitmap.png", qrBitmapBytes);
                    Console.WriteLine("QR code (from Bitmap) generated as qrcode_from_bitmap.png");
                }
                qrBitmap.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating QR code: {ex.Message}");
        }
    }
}
*/