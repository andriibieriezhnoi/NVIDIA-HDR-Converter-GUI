using System.Runtime.CompilerServices;

namespace NHC.Core.ToneMapping;

/// <summary>
/// Narkowicz fitted ACES approximation (RRT+ODT). Numerically stable, widely
/// used in games, preserves highlight rolloff well.
/// </summary>
internal sealed class AcesToneMapper : ToneMapperBase
{
    public override ToneMapperKind Kind => ToneMapperKind.Aces;

    private const float A = 2.51f;
    private const float B = 0.03f;
    private const float C = 2.43f;
    private const float D = 0.59f;
    private const float E = 0.14f;

    protected override void MapPixel(float r, float g, float b, out float or, out float og, out float ob)
    {
        or = Curve(r);
        og = Curve(g);
        ob = Curve(b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Curve(float v)
    {
        var numerator = v * (A * v + B);
        var denominator = v * (C * v + D) + E;
        return denominator <= 0f ? 0f : Clamp01(numerator / denominator);
    }
}
