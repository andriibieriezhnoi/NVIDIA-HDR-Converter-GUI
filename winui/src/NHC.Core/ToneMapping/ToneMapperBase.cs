using System.Runtime.CompilerServices;
using NHC.Core.Imaging;

namespace NHC.Core.ToneMapping;

internal abstract class ToneMapperBase : IToneMapper
{
    public abstract ToneMapperKind Kind { get; }

    public HdrImage Apply(HdrImage input, ToneMapperSettings settings)
    {
        ArgumentNullException.ThrowIfNull(input);

        var output = input.CloneShape();
        var src = input.Pixels;
        var dst = output.Pixels;
        var exposure = settings.Exposure;
        var invGamma = settings.Gamma <= 0f ? 1f : 1f / settings.Gamma;

        // Parallelise per-row; tone mappers are embarrassingly parallel.
        Parallel.For(0, input.Height, y =>
        {
            var rowStart = y * input.Width * 3;
            var rowEnd = rowStart + input.Width * 3;
            for (var i = rowStart; i < rowEnd; i += 3)
            {
                var r = ApplyPreGain(src[i], exposure, invGamma);
                var g = ApplyPreGain(src[i + 1], exposure, invGamma);
                var b = ApplyPreGain(src[i + 2], exposure, invGamma);
                MapPixel(r, g, b, out dst[i], out dst[i + 1], out dst[i + 2]);
            }
        });

        return output;
    }

    protected abstract void MapPixel(float r, float g, float b, out float or, out float og, out float ob);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyPreGain(float v, float exposure, float invGamma)
    {
        v = MathF.Max(v, 0f) * exposure;
        return invGamma == 1f ? v : MathF.Pow(v, invGamma);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static float Luminance(float r, float g, float b) =>
        0.2126f * r + 0.7152f * g + 0.0722f * b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
}
