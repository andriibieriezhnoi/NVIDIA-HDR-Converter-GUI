namespace NHC.Core.Imaging;

/// <summary>
/// A tone-mapped, gamma-encoded 8-bit sRGB image. Layout: interleaved RGB,
/// stride = width * 3. Produced as the final step before encoding to JPEG.
/// </summary>
public sealed class SdrImage
{
    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public SdrImage(int width, int height, byte[] pixels)
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
    }
}
