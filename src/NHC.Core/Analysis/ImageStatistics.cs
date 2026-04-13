using NHC.Core.Imaging;

namespace NHC.Core.Analysis;

/// <summary>
/// Summary statistics used by <see cref="ToneMapping.AutoToneMapSelector"/>
/// and surfaced in the UI's "image info" panel.
/// </summary>
public readonly record struct ImageStatistics(
    float MinLuminance,
    float MaxLuminance,
    float MeanLuminance,
    float DynamicRangeStops,
    float ShadowFraction,
    float MidtoneFraction,
    float HighlightFraction,
    float Saturation)
{
    public static ImageStatistics Compute(HdrImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var px = image.Pixels;
        var n = image.PixelCount;

        float min = float.PositiveInfinity;
        float max = 0f;
        double sumLum = 0;
        double sumSat = 0;
        int shadow = 0, midtone = 0, highlight = 0;

        for (int i = 0, p = 0; i < n; i++, p += 3)
        {
            var r = px[p];
            var g = px[p + 1];
            var b = px[p + 2];
            var lum = 0.2126f * r + 0.7152f * g + 0.0722f * b;

            if (lum > 0f && lum < min) min = lum;
            if (lum > max) max = lum;
            sumLum += lum;

            // Zone system: shadow < 0.1, midtone 0.1-1.0, highlight > 1.0 (HDR-aware buckets).
            if (lum < 0.1f) shadow++;
            else if (lum <= 1.0f) midtone++;
            else highlight++;

            var cMax = MathF.Max(r, MathF.Max(g, b));
            var cMin = MathF.Min(r, MathF.Min(g, b));
            sumSat += cMax > 0f ? (cMax - cMin) / cMax : 0f;
        }

        if (float.IsPositiveInfinity(min)) min = 0f;

        var dynRange = (min > 0f && max > min)
            ? MathF.Log2(max / min)
            : 0f;

        var inv = 1.0 / n;
        return new ImageStatistics(
            MinLuminance: min,
            MaxLuminance: max,
            MeanLuminance: (float)(sumLum * inv),
            DynamicRangeStops: dynRange,
            ShadowFraction: (float)(shadow * inv),
            MidtoneFraction: (float)(midtone * inv),
            HighlightFraction: (float)(highlight * inv),
            Saturation: (float)(sumSat * inv));
    }
}
