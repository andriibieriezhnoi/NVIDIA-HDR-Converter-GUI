using NHC.Core.Analysis;
using NHC.Core.Imaging;

namespace NHC.Core.ToneMapping;

/// <summary>
/// Chooses a tone mapper from the v1 set based on image statistics.
/// The scoring is intentionally simple and testable — no hand-tuned weights
/// that would drift algorithm selection between versions.
/// </summary>
public static class AutoToneMapSelector
{
    public static ToneMapperKind Select(HdrImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var stats = ImageStatistics.Compute(image);
        return Select(stats);
    }

    public static ToneMapperKind Select(ImageStatistics stats)
    {
        // High dynamic range with deep shadows → Hable's filmic rolloff.
        if (stats.DynamicRangeStops >= 12f && stats.ShadowFraction > 0.25f)
        {
            return ToneMapperKind.Hable;
        }

        // Hard-clipped highlights or very bright scene → ACES preserves highlight colour best.
        if (stats.HighlightFraction > 0.15f || stats.MaxLuminance > 8f)
        {
            return ToneMapperKind.Aces;
        }

        // Moderate range, balanced histogram → extended Reinhard with white point.
        return ToneMapperKind.ReinhardExtended;
    }
}
