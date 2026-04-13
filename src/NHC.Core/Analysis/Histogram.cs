using NHC.Core.Imaging;

namespace NHC.Core.Analysis;

/// <summary>
/// Per-channel 256-bin histogram of a gamma-encoded 8-bit image. Used by the
/// before/after histogram control in the UI. Kept in NHC.Core so it's usable
/// from both Win2D rendering and unit tests.
/// </summary>
public sealed class Histogram
{
    public int[] Red { get; }

    public int[] Green { get; }

    public int[] Blue { get; }

    public int Peak { get; }

    public const int BinCount = 256;

    private Histogram(int[] r, int[] g, int[] b, int peak)
    {
        Red = r;
        Green = g;
        Blue = b;
        Peak = peak;
    }

    public static Histogram Compute(SdrImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var r = new int[BinCount];
        var g = new int[BinCount];
        var b = new int[BinCount];
        var px = image.Pixels;
        for (var i = 0; i < px.Length; i += 3)
        {
            r[px[i]]++;
            g[px[i + 1]]++;
            b[px[i + 2]]++;
        }

        var peak = 0;
        for (var i = 0; i < BinCount; i++)
        {
            if (r[i] > peak) peak = r[i];
            if (g[i] > peak) peak = g[i];
            if (b[i] > peak) peak = b[i];
        }

        return new Histogram(r, g, b, peak);
    }
}
