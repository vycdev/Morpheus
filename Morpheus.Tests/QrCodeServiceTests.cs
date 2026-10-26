using Morpheus.Utilities.Images;
using SkiaSharp;

namespace Morpheus.Tests;

public class QrCodeServiceTests
{
    [Fact]
    public void GenerateQrCodeWithColors_ProducesReadablePngWithRequestedColorsAndQuietZone()
    {
        const int pixelsPerModule = 4;
        const string data = "https://example.com";
        const string dark = "#123456";
        const string light = "#FEDCBA";

        using SKBitmap withQuietZone = SKBitmap.Decode(
            QrCodeService.GenerateQrCodeWithColors(data, dark, light, pixelsPerModule));
        using SKBitmap withoutQuietZone = SKBitmap.Decode(
            QrCodeService.GenerateQrCodeWithColors(data, dark, light, pixelsPerModule,
                drawQuietZones: false));

        Assert.Equal(withQuietZone.Width, withQuietZone.Height);
        Assert.Equal(withoutQuietZone.Width, withoutQuietZone.Height);
        Assert.Equal(8 * pixelsPerModule, withQuietZone.Width - withoutQuietZone.Width);
        Assert.Equal(new SKColor(0xFE, 0xDC, 0xBA), withQuietZone.GetPixel(0, 0));
        Assert.Equal(new SKColor(0x12, 0x34, 0x56), withoutQuietZone.GetPixel(0, 0));
    }
}
