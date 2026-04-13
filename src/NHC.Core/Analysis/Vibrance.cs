using NHC.Core.Imaging;

namespace NHC.Core.Analysis;

/// <summary>
/// Vibrance — a saturation boost that protects pixels that are already close
/// to fully saturated. Operates per-pixel on linear-light [0,1] RGB. Neutral
/// (R=G=B) pixels stay neutral; fully-saturated pixels are untouched.
/// </summary>
public static class Vibrance
{
    /// <summary>
    /// Apply vibrance to a linear-light [0,1] image. <paramref name="strength"/>
    /// is in [0, 1]; 0 is a no-op, 1 roughly doubles the chroma of grey-ish
    /// pixels while leaving already-vivid ones alone.
    /// </summary>
    public static HdrImage Apply(HdrImage input, float strength)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (strength <= 0f) return input;
        strength = MathF.Min(strength, 1f);

        var w = input.Width;
        var h = input.Height;
        var src = input.Pixels;
        var output = input.CloneShape();
        var dst = output.Pixels;

        Parallel.For(0, h, y =>
        {
            var row = y * w * 3;
            for (var x = 0; x < w; x++)
            {
                var i = row + x * 3;
                var r = src[i];
                var g = src[i + 1];
                var b = src[i + 2];

                var cMax = MathF.Max(r, MathF.Max(g, b));
                var cMin = MathF.Min(r, MathF.Min(g, b));

                // Saturation proxy in [0,1]. For near-black pixels cMax~0 → treat as fully unsaturated.
                var saturation = cMax > 1e-6f ? (cMax - cMin) / cMax : 0f;

                // Boost chroma less when already saturated. strength=1, sat=0 → 2x chroma.
                var boost = 1f + strength * (1f - saturation);

                // Expand channels around their mean (luminance-preserving-ish).
                var mean = (r + g + b) * (1f / 3f);
                dst[i] = mean + (r - mean) * boost;
                dst[i + 1] = mean + (g - mean) * boost;
                dst[i + 2] = mean + (b - mean) * boost;
            }
        });

        return output;
    }
}
