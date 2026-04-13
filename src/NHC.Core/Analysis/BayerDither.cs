using NHC.Core.Imaging;

namespace NHC.Core.Analysis;

/// <summary>
/// Ordered 8x8 Bayer dithering applied to a linear image before quantisation.
/// Breaks up banding in smooth gradients when rounding float → 8-bit.
/// </summary>
public static class BayerDither
{
    // 8x8 Bayer matrix normalised to (-0.5, +0.5).
    private static readonly float[,] Matrix = BuildMatrix();

    private const int Size = 8;

    public static SdrImage EncodeToSrgb(HdrImage linear01, float strength = 1f / 255f)
    {
        ArgumentNullException.ThrowIfNull(linear01);
        var width = linear01.Width;
        var height = linear01.Height;
        var src = linear01.Pixels;
        var dst = new byte[width * height * 3];

        Parallel.For(0, height, y =>
        {
            var row = y & (Size - 1);
            var srcRowStart = y * width * 3;
            var dstRowStart = srcRowStart;
            for (var x = 0; x < width; x++)
            {
                var col = x & (Size - 1);
                var noise = Matrix[row, col] * strength;
                var idx = srcRowStart + x * 3;
                dst[dstRowStart + x * 3] = Quantise(src[idx], noise);
                dst[dstRowStart + x * 3 + 1] = Quantise(src[idx + 1], noise);
                dst[dstRowStart + x * 3 + 2] = Quantise(src[idx + 2], noise);
            }
        });

        return new SdrImage(width, height, dst);
    }

    private static byte Quantise(float linear, float noise)
    {
        var encoded = Srgb.LinearToEncoded(linear) + noise;
        var quantised = MathF.Round(encoded * 255f);
        if (quantised < 0f) return 0;
        if (quantised > 255f) return 255;
        return (byte)quantised;
    }

    private static float[,] BuildMatrix()
    {
        // Canonical 8x8 Bayer: recursive interleave of the 2x2 base.
        var m = new float[Size, Size];
        var baseMatrix = new[,]
        {
            {  0, 32,  8, 40,  2, 34, 10, 42 },
            { 48, 16, 56, 24, 50, 18, 58, 26 },
            { 12, 44,  4, 36, 14, 46,  6, 38 },
            { 60, 28, 52, 20, 62, 30, 54, 22 },
            {  3, 35, 11, 43,  1, 33,  9, 41 },
            { 51, 19, 59, 27, 49, 17, 57, 25 },
            { 15, 47,  7, 39, 13, 45,  5, 37 },
            { 63, 31, 55, 23, 61, 29, 53, 21 },
        };

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                // Normalise to (-0.5, +0.5) so dither is zero-mean.
                m[y, x] = (baseMatrix[y, x] / 64f) - 0.5f;
            }
        }

        return m;
    }
}
