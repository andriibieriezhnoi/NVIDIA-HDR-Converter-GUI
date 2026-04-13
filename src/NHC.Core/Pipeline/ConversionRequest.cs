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
    float ClaheStrength,
    float VibranceStrength,
    float EdgeStrength);

public enum OutputFormat
{
    Jpeg,
    Tiff,
    JpegAndTiff,
}
