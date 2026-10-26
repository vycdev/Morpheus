using System.Buffers.Binary;
using Morpheus.Modules;

namespace Morpheus.Tests;

public class ImageModuleTests
{
    [Fact]
    public void TryDownscaleImage_ReturnsFalseForInvalidImageData()
    {
        bool result = ImageModule.TryDownscaleImage([0, 1, 2], out byte[] output);

        Assert.False(result);
        Assert.Empty(output);
    }

    [Fact]
    public async Task TryDownscaleImage_RejectsTruncatedBigTiffWithoutLoopingOverDeclaredEntries()
    {
        // A BigTIFF directory claims billions of entries but contains none.
        // GHSA-wmxv-xphr-5c9g made older decoders loop without consuming input.
        byte[] input = new byte[24];
        input[0] = input[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(input.AsSpan(2), 43);
        BinaryPrimitives.WriteUInt16LittleEndian(input.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt64LittleEndian(input.AsSpan(8), 16);
        BinaryPrimitives.WriteUInt64LittleEndian(input.AsSpan(16), 5_000_000_000);

        (bool success, byte[] output) = await Task.Run(() =>
        {
            bool success = ImageModule.TryDownscaleImage(input, out byte[] output);
            return (success, output);
        }).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(success);
        Assert.Empty(output);
    }
}
