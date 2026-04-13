using NHC.Core.Imaging;

namespace NHC.Core.ToneMapping;

/// <summary>
/// Reduces an HDR linear-light image to a [0,1] linear-light image suitable
/// for sRGB encoding. Implementations must be pure and stateless so the
/// pipeline can parallelise batch processing safely.
/// </summary>
public interface IToneMapper
{
    ToneMapperKind Kind { get; }

    /// <summary>Produce a new [0,1] linear image. Metadata propagates unchanged.</summary>
    HdrImage Apply(HdrImage input, ToneMapperSettings settings);
}

public enum ToneMapperKind
{
    /// <summary>Selects the best mapper for the scene via <see cref="AutoToneMapSelector"/>.</summary>
    Auto,
    ReinhardExtended,
    Aces,
    Hable,
}

public readonly record struct ToneMapperSettings(
    float Exposure,
    float Gamma,
    float DisplayPeakLuminance)
{
    public static ToneMapperSettings Default => new(Exposure: 1.0f, Gamma: 1.0f, DisplayPeakLuminance: 1000f);
}
