using NHC.Core.Imaging;

namespace NHC.Core.Analysis;

/// <summary>
/// Contrast-Limited Adaptive Histogram Equalization on the luminance channel.
/// Divides the image into an 8×8 grid, builds a histogram per tile, clips it
/// at a strength-controlled limit (redistributing the excess), and bilinearly
/// interpolates the per-tile CDFs across the image. RGB is scaled by the
/// luminance ratio so hues are preserved.
/// </summary>
public static class Clahe
{
    private const int TilesX = 8;
    private const int TilesY = 8;
    private const int Bins = 64;

    /// <summary>
    /// Apply CLAHE to a linear-light [0,1] image. <paramref name="strength"/>
    /// is in [0, 1]; 0 is a no-op, 1 applies the full contrast limit.
    /// </summary>
    public static HdrImage Apply(HdrImage input, float strength)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (strength <= 0f) return input;
        strength = MathF.Min(strength, 1f);

        var w = input.Width;
        var h = input.Height;
        if (w < TilesX * 2 || h < TilesY * 2)
        {
            // Image is too small to tile meaningfully — skip rather than degrade.
            return input;
        }

        var src = input.Pixels;

        // --- Step 1: per-tile luminance histogram, clipped CDF. -------------
        var tileW = w / TilesX;
        var tileH = h / TilesY;
        var cdfs = new float[TilesX * TilesY][];

        Parallel.For(0, TilesX * TilesY, t =>
        {
            var tx = t % TilesX;
            var ty = t / TilesX;
            var x0 = tx * tileW;
            var y0 = ty * tileH;
            var x1 = tx == TilesX - 1 ? w : x0 + tileW;
            var y1 = ty == TilesY - 1 ? h : y0 + tileH;

            var hist = new int[Bins];
            for (var y = y0; y < y1; y++)
            {
                var row = y * w * 3;
                for (var x = x0; x < x1; x++)
                {
                    var i = row + x * 3;
                    var lum = 0.2126f * src[i] + 0.7152f * src[i + 1] + 0.0722f * src[i + 2];
                    var clamped = Math.Clamp(lum, 0f, 1f);
                    var bin = (int)(clamped * (Bins - 1) + 0.5f);
                    hist[bin]++;
                }
            }

            var pixelCount = (x1 - x0) * (y1 - y0);
            // Clip limit: strength=1 caps bins at 4× average count, strength=0 ~ no clip.
            var average = pixelCount / (float)Bins;
            var clipLimit = (int)MathF.Ceiling(average * (1f + 3f * strength));
            var excess = 0;
            for (var b = 0; b < Bins; b++)
            {
                if (hist[b] > clipLimit)
                {
                    excess += hist[b] - clipLimit;
                    hist[b] = clipLimit;
                }
            }
            // Redistribute uniformly.
            var redistribute = excess / Bins;
            var remainder = excess % Bins;
            for (var b = 0; b < Bins; b++)
            {
                hist[b] += redistribute;
            }
            for (var b = 0; b < remainder; b++)
            {
                hist[b]++;
            }

            var cdf = new float[Bins];
            var running = 0;
            for (var b = 0; b < Bins; b++)
            {
                running += hist[b];
                cdf[b] = pixelCount > 0 ? running / (float)pixelCount : 0f;
            }
            cdfs[t] = cdf;
        });

        // --- Step 2: remap each pixel by bilinearly interpolating 4 tile CDFs. --
        var output = input.CloneShape();
        var dst = output.Pixels;
        var halfTileW = tileW * 0.5f;
        var halfTileH = tileH * 0.5f;

        Parallel.For(0, h, y =>
        {
            var ty = (y - halfTileH) / tileH;
            var ty0 = Math.Clamp((int)MathF.Floor(ty), 0, TilesY - 1);
            var ty1 = Math.Clamp(ty0 + 1, 0, TilesY - 1);
            var fy = Math.Clamp(ty - ty0, 0f, 1f);

            for (var x = 0; x < w; x++)
            {
                var tx = (x - halfTileW) / tileW;
                var tx0 = Math.Clamp((int)MathF.Floor(tx), 0, TilesX - 1);
                var tx1 = Math.Clamp(tx0 + 1, 0, TilesX - 1);
                var fx = Math.Clamp(tx - tx0, 0f, 1f);

                var i = (y * w + x) * 3;
                var r = src[i];
                var g = src[i + 1];
                var b = src[i + 2];
                var lum = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                var clamped = Math.Clamp(lum, 0f, 1f);
                var bin = (int)(clamped * (Bins - 1) + 0.5f);

                var v00 = cdfs[ty0 * TilesX + tx0][bin];
                var v10 = cdfs[ty0 * TilesX + tx1][bin];
                var v01 = cdfs[ty1 * TilesX + tx0][bin];
                var v11 = cdfs[ty1 * TilesX + tx1][bin];

                var top = v00 * (1f - fx) + v10 * fx;
                var bot = v01 * (1f - fx) + v11 * fx;
                var newLum = top * (1f - fy) + bot * fy;

                // Blend between identity and equalized by strength so low values act as "partial CLAHE".
                var targetLum = lum * (1f - strength) + newLum * strength;
                var ratio = lum > 1e-6f ? targetLum / lum : 1f;

                dst[i] = r * ratio;
                dst[i + 1] = g * ratio;
                dst[i + 2] = b * ratio;
            }
        });

        return output;
    }
}
