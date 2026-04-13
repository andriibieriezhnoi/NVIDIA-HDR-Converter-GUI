using System.Runtime.InteropServices;
using NHC.Core.Abstractions;
using NHC.Core.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace NHC.Imaging.Windows;

/// <summary>
/// Writes a 16-bit per channel LZW-compressed TIFF. Input is a linear [0,1]
/// <see cref="HdrImage"/> which we quantise to 16-bit UNORM — the TIFF stays
/// linear, preserving more precision than the JPEG output.
/// </summary>
public sealed class WicTiffEncoder : IHdrEncoder
{
    public string FileExtension => ".tif";

    public async ValueTask EncodeAsync(HdrImage image, string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ct.ThrowIfCancellationRequested();

        var pixelCount = image.Width * image.Height;
        var src = image.Pixels;

        // Rgba16 UNORM: 4 channels × 16 bits. Alpha = 65535.
        var rgba16 = new ushort[pixelCount * 4];
        for (int i = 0, o = 0; i < pixelCount; i++, o += 4)
        {
            var s = i * 3;
            rgba16[o] = To16(src[s]);
            rgba16[o + 1] = To16(src[s + 1]);
            rgba16[o + 2] = To16(src[s + 2]);
            rgba16[o + 3] = 65535;
        }

        var bytes = MemoryMarshal.AsBytes<ushort>(rgba16).ToArray();

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(filePath)!)
            .AsTask(ct).ConfigureAwait(false);
        var file = await folder.CreateFileAsync(Path.GetFileName(filePath), CreationCollisionOption.ReplaceExisting)
            .AsTask(ct).ConfigureAwait(false);

        using var outStream = await file.OpenAsync(FileAccessMode.ReadWrite).AsTask(ct).ConfigureAwait(false);
        var props = new BitmapPropertySet
        {
            // TIFF LZW compression.
            { "TiffCompressionMethod", new BitmapTypedValue(TiffCompressionMode.Lzw, Windows.Foundation.PropertyType.UInt8) },
        };
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.TiffEncoderId, outStream, props)
            .AsTask(ct).ConfigureAwait(false);

        encoder.SetPixelData(
            BitmapPixelFormat.Rgba16,
            BitmapAlphaMode.Ignore,
            (uint)image.Width,
            (uint)image.Height,
            96.0,
            96.0,
            bytes);

        await encoder.FlushAsync().AsTask(ct).ConfigureAwait(false);
    }

    private static ushort To16(float linear)
    {
        if (linear <= 0f) return 0;
        if (linear >= 1f) return 65535;
        return (ushort)MathF.Round(linear * 65535f);
    }

    // WIC TIFF compression enum — cast to byte; values documented at
    // https://learn.microsoft.com/en-us/windows/win32/wic/-wic-native-image-format-metadata-queries.
    private enum TiffCompressionMode : byte
    {
        DontCare = 0,
        None = 1,
        Ccitt3 = 2,
        Ccitt4 = 3,
        Lzw = 4,
        Rle = 5,
        Zip = 6,
        LzwhDifferencing = 7,
    }
}
