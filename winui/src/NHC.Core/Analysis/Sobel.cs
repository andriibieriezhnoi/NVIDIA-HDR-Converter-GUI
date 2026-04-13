using NHC.Core.Imaging;

namespace NHC.Core.Analysis;

/// <summary>
/// Sobel edge detector and adaptive edge enhancer operating on linear-light
/// luminance. Ported from the Python <c>EdgeEnhancementBlock</c> but without
/// the neural parameters — edge strength is a user-controlled scalar.
/// </summary>
public static class Sobel
{
    /// <summary>
    /// Enhance edges in-place on a linear [0,1] image. <paramref name="strength"/>
    /// is in [0, 1]; 0 is a no-op, 1 applies the full Sobel response capped at
    /// 20% of local luminance to avoid haloing.
    /// </summary>
    public static HdrImage Enhance(HdrImage input, float strength)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (strength <= 0f) return input;
        strength = MathF.Min(strength, 1f);

        var w = input.Width;
        var h = input.Height;
        var src = input.Pixels;
        var output = input.CloneShape();
        var dst = output.Pixels;
        Array.Copy(src, dst, src.Length);

        // Interior pixels only; borders are left untouched (cheap and visually imperceptible).
        Parallel.For(1, h - 1, y =>
        {
            for (var x = 1; x < w - 1; x++)
            {
                var gx = Lum(src, x + 1, y - 1, w) - Lum(src, x - 1, y - 1, w)
                       + 2f * (Lum(src, x + 1, y, w) - Lum(src, x - 1, y, w))
                       + Lum(src, x + 1, y + 1, w) - Lum(src, x - 1, y + 1, w);

                var gy = Lum(src, x - 1, y + 1, w) - Lum(src, x - 1, y - 1, w)
                       + 2f * (Lum(src, x, y + 1, w) - Lum(src, x, y - 1, w))
                       + Lum(src, x + 1, y + 1, w) - Lum(src, x + 1, y - 1, w);

                var magnitude = MathF.Sqrt(gx * gx + gy * gy);
                var idx = (y * w + x) * 3;
                var localLum = Lum(src, x, y, w);
                var cap = 0.2f * localLum;
                var delta = MathF.Min(magnitude, cap) * strength;

                dst[idx] = MathF.Min(1f, src[idx] + delta);
                dst[idx + 1] = MathF.Min(1f, src[idx + 1] + delta);
                dst[idx + 2] = MathF.Min(1f, src[idx + 2] + delta);
            }
        });

        return output;
    }

    private static float Lum(float[] pixels, int x, int y, int width)
    {
        var i = (y * width + x) * 3;
        return 0.2126f * pixels[i] + 0.7152f * pixels[i + 1] + 0.0722f * pixels[i + 2];
    }
}
