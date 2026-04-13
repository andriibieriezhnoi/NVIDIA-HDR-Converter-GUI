using System.Runtime.CompilerServices;

namespace NHC.Core.ToneMapping;

/// <summary>
/// Reinhard operator extended with a user-settable white point, applied per-channel
/// on a luminance-preserving curve. Good default for most HDR screenshots.
/// </summary>
internal sealed class ReinhardExtendedToneMapper : ToneMapperBase
{
    public override ToneMapperKind Kind => ToneMapperKind.ReinhardExtended;

    private const float WhitePoint = 4.0f;
    private const float WhitePointSquared = WhitePoint * WhitePoint;

    protected override void MapPixel(float r, float g, float b, out float or, out float og, out float ob)
    {
        or = Curve(r);
        og = Curve(g);
        ob = Curve(b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Curve(float v)
    {
        var numerator = v * (1f + v / WhitePointSquared);
        var denominator = 1f + v;
        return denominator <= 0f ? 0f : Clamp01(numerator / denominator);
    }
}
