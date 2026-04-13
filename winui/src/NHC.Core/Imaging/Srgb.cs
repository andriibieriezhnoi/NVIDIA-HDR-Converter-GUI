using System.Runtime.CompilerServices;

namespace NHC.Core.Imaging;

/// <summary>
/// sRGB opto-electronic transfer functions (IEC 61966-2-1). Linear light → gamma-encoded and back.
/// </summary>
public static class Srgb
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LinearToEncoded(float linear)
    {
        if (linear <= 0f) return 0f;
        if (linear >= 1f) return 1f;
        return linear <= 0.0031308f
            ? 12.92f * linear
            : 1.055f * MathF.Pow(linear, 1f / 2.4f) - 0.055f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EncodedToLinear(float encoded)
    {
        if (encoded <= 0f) return 0f;
        if (encoded >= 1f) return 1f;
        return encoded <= 0.04045f
            ? encoded / 12.92f
            : MathF.Pow((encoded + 0.055f) / 1.055f, 2.4f);
    }
}
