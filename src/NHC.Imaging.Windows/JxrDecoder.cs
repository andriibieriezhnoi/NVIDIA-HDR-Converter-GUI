using System.Runtime.InteropServices.WindowsRuntime;
using NHC.Core.Abstractions;
using NHC.Core.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace NHC.Imaging.Windows;

/// <summary>
/// Decodes JPEG XR (.jxr) screenshots written by NVIDIA Ansel using the
/// Windows Imaging Component stack. Output is linear scRGB float32 RGB.
///
/// NVIDIA Ansel writes scRGB-linear JXR (1.0 = 80 nits, negatives legal). We
/// read the half-float pixel format and convert to full float32 without any
/// gamma curve — the tone mapper is the only stage allowed to touch intensity.
/// </summary>
public sealed class JxrDecoder : IHdrDecoder
{
    public async ValueTask<HdrImage> DecodeAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ct.ThrowIfCancellationRequested();

        var file = await StorageFile.GetFileFromPathAsync(filePath).AsTask(ct).ConfigureAwait(false);
        using var stream = await file.OpenReadAsync().AsTask(ct).ConfigureAwait(false);
        var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.JpegXRDecoderId, stream)
            .AsTask(ct).ConfigureAwait(false);

        var width = (int)decoder.PixelWidth;
        var height = (int)decoder.PixelHeight;

        // Request half-float RGBA premultiplied-off so scRGB values survive intact.
        var transform = new BitmapTransform();
        var pixelDataProvider = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Rgba16,
            BitmapAlphaMode.Ignore,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage).AsTask(ct).ConfigureAwait(false);

        var halfBytes = pixelDataProvider.DetachPixelData();
        ct.ThrowIfCancellationRequested();

        // WIC returns half-precision floats when the codec is JPEG XR with a
        // float pixel format even if we asked for Rgba16 (which is UNORM).
        // We defensively treat the bytes as half-float. If the codec gave us
        // UNORM, we detect it by peeking at the metadata query.
        var isHalfFloat = await IsHalfFloatAsync(decoder).ConfigureAwait(false);
        var pixels = isHalfFloat
            ? HalfFloatToLinearFloat(halfBytes, width, height)
            : UnormToLinearFloat(halfBytes, width, height);

        return new HdrImage(width, height, pixels, HdrMetadata.Default);
    }

    private static async Task<bool> IsHalfFloatAsync(BitmapDecoder decoder)
    {
        try
        {
            // /ifd/PixelFormat returns a GUID string like {guid}. 64bppRGBAHalf GUID:
            // 0x6fddc324-4e03-4bfe-b185-3d77768dc90f  (WICPixelFormat64bppRGBAHalf)
            var props = await decoder.BitmapProperties.GetPropertiesAsync(new[] { "/ifd/PixelFormat" });
            if (props.TryGetValue("/ifd/PixelFormat", out var pixelFormatValue)
                && pixelFormatValue?.Value is string guidString)
            {
                return guidString.Contains("6fddc324", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            // Property not present → fall back to assuming half-float for JXR HDR.
        }
        return true;
    }

    private static float[] HalfFloatToLinearFloat(byte[] halfBytes, int width, int height)
    {
        var pixelCount = width * height;
        var output = new float[pixelCount * 3];
        var span = MemoryMarshal.Cast<byte, Half>(halfBytes);
        // Layout: RGBA half-float per pixel (8 bytes).
        for (int i = 0, o = 0, s = 0; i < pixelCount; i++, o += 3, s += 4)
        {
            output[o] = (float)span[s];
            output[o + 1] = (float)span[s + 1];
            output[o + 2] = (float)span[s + 2];
        }
        return output;
    }

    private static float[] UnormToLinearFloat(byte[] unormBytes, int width, int height)
    {
        var pixelCount = width * height;
        var output = new float[pixelCount * 3];
        var span = MemoryMarshal.Cast<byte, ushort>(unormBytes);
        const float scale = 1f / 65535f;
        for (int i = 0, o = 0, s = 0; i < pixelCount; i++, o += 3, s += 4)
        {
            // UNORM-16 is gamma-encoded sRGB; decode to linear.
            output[o] = Srgb.EncodedToLinear(span[s] * scale);
            output[o + 1] = Srgb.EncodedToLinear(span[s + 1] * scale);
            output[o + 2] = Srgb.EncodedToLinear(span[s + 2] * scale);
        }
        return output;
    }
}
