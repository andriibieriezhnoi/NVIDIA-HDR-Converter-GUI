using System.Runtime.CompilerServices;

namespace NHC.Core.Imaging;

/// <summary>
/// A 32-bit float linear-light scRGB HDR image in planar RGB layout with
/// interleaved R, G, B channels (stride = width * 3). Values may exceed 1.0
/// and may be negative, as per the scRGB color space.
/// </summary>
public sealed class HdrImage
{
    public int Width { get; }

    public int Height { get; }

    /// <summary>Interleaved linear-light RGB, length = Width * Height * 3.</summary>
    public float[] Pixels { get; }

    /// <summary>Optional HDR metadata parsed from the source container.</summary>
    public HdrMetadata Metadata { get; }

    public HdrImage(int width, int height, float[] pixels, HdrMetadata metadata)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != checked(width * height * 3))
        {
            throw new ArgumentException(
                $"Pixel buffer length {pixels.Length} does not match {width}x{height}x3.",
                nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
        Metadata = metadata;
    }

    public int PixelCount => Width * Height;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PixelIndex(int x, int y) => (y * Width + x) * 3;

    /// <summary>Allocates a new HdrImage sharing metadata but with fresh pixel storage.</summary>
    public HdrImage CloneShape() => new(Width, Height, new float[Pixels.Length], Metadata);
}
