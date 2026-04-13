using NHC.Core.ToneMapping;

namespace NHC.Core.Pipeline;

/// <summary>
/// A single conversion job. Immutable so it's safe to queue across threads.
/// </summary>
public sealed record ConversionRequest(
    string InputPath,
    string OutputPath,
    OutputFormat Format,
    ToneMapperKind ToneMapper,
    ToneMapperSettings ToneMapperSettings,
    bool EnableEnhancement,
    float EnhancementStrength,
    float EdgeStrength);

public enum OutputFormat
{
    Jpeg,
    Tiff,
    JpegAndTiff,
}
