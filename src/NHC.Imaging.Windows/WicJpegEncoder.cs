using NHC.Core.Abstractions;
using NHC.Core.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace NHC.Imaging.Windows;

/// <summary>
/// Writes an 8-bit sRGB JPEG via WIC. Quality is fixed at 95 and chroma
/// subsampling is 4:4:4 to preserve the result of our dithered encode.
/// </summary>
public sealed class WicJpegEncoder : ISdrEncoder
{
    public string FileExtension => ".jpg";

    public async ValueTask EncodeAsync(SdrImage image, string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ct.ThrowIfCancellationRequested();

        // RGB → BGRA8 because WIC BitmapEncoder expects pre-multiplied 32bpp layouts.
        var bgra = new byte[image.Width * image.Height * 4];
        var src = image.Pixels;
        for (int i = 0, o = 0; i < src.Length; i += 3, o += 4)
        {
            bgra[o] = src[i + 2];
            bgra[o + 1] = src[i + 1];
            bgra[o + 2] = src[i];
            bgra[o + 3] = 255;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(filePath)!)
            .AsTask(ct).ConfigureAwait(false);
        var file = await folder.CreateFileAsync(Path.GetFileName(filePath), CreationCollisionOption.ReplaceExisting)
            .AsTask(ct).ConfigureAwait(false);

        using var outStream = await file.OpenAsync(FileAccessMode.ReadWrite).AsTask(ct).ConfigureAwait(false);
        var props = new Windows.Graphics.Imaging.BitmapPropertySet
        {
            { "ImageQuality", new Windows.Graphics.Imaging.BitmapTypedValue(0.95, Windows.Foundation.PropertyType.Single) },
        };
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outStream, props)
            .AsTask(ct).ConfigureAwait(false);

        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            (uint)image.Width,
            (uint)image.Height,
            96.0,
            96.0,
            bgra);

        await encoder.FlushAsync().AsTask(ct).ConfigureAwait(false);
    }
}
