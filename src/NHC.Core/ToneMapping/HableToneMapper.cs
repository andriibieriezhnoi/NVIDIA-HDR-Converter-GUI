using System.Runtime.CompilerServices;

namespace NHC.Core.ToneMapping;

/// <summary>
/// John Hable's "Uncharted 2" filmic curve. Favours contrasty, cinematic results.
/// </summary>
internal sealed class HableToneMapper : ToneMapperBase
{
    public override ToneMapperKind Kind => ToneMapperKind.Hable;

    private const float A = 0.15f;
    private const float B = 0.50f;
    private const float C = 0.10f;
    private const float D = 0.20f;
    private const float E = 0.02f;
    private const float F = 0.30f;
    private const float WhiteScaleInput = 11.2f;

    private static readonly float WhiteScale = 1f / Curve(WhiteScaleInput);

    protected override void MapPixel(float r, float g, float b, out float or, out float og, out float ob)
    {
        or = Clamp01(Curve(r) * WhiteScale);
        og = Clamp01(Curve(g) * WhiteScale);
        ob = Clamp01(Curve(b) * WhiteScale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Curve(float x)
    {
        var numerator = x * (A * x + C * B) + D * E;
        var denominator = x * (A * x + B) + D * F;
        return denominator <= 0f ? 0f : numerator / denominator - E / F;
    }
}
