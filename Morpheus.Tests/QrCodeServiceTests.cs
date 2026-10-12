using Morpheus.Utilities.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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

        using Image<Rgba32> withQuietZone = Image.Load<Rgba32>(
            QrCodeService.GenerateQrCodeWithColors(data, dark, light, pixelsPerModule));
        using Image<Rgba32> withoutQuietZone = Image.Load<Rgba32>(
            QrCodeService.GenerateQrCodeWithColors(data, dark, light, pixelsPerModule,
                drawQuietZones: false));

        Assert.Equal(withQuietZone.Width, withQuietZone.Height);
        Assert.Equal(withoutQuietZone.Width, withoutQuietZone.Height);
        Assert.Equal(8 * pixelsPerModule, withQuietZone.Width - withoutQuietZone.Width);
        Assert.Equal(new Rgba32(0xFE, 0xDC, 0xBA), withQuietZone[0, 0]);
        Assert.Equal(new Rgba32(0x12, 0x34, 0x56), withoutQuietZone[0, 0]);
    }
}
