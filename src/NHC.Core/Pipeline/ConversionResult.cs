using NHC.Core.Analysis;
using NHC.Core.Imaging;
using NHC.Core.ToneMapping;

namespace NHC.Core.Pipeline;

/// <summary>
/// Outcome of a completed pipeline run. Success or failure, plus diagnostics
/// useful for UI previews (histograms, image statistics, chosen algorithm).
/// </summary>
public sealed record ConversionResult(
    bool Success,
    string InputPath,
    IReadOnlyList<string> OutputPaths,
    ToneMapperKind ResolvedToneMapper,
    ImageStatistics? SourceStatistics,
    Histogram? OutputHistogram,
    TimeSpan Elapsed,
    string? ErrorMessage)
{
    public static ConversionResult Ok(
        string inputPath,
        IReadOnlyList<string> outputs,
        ToneMapperKind resolved,
        ImageStatistics stats,
        Histogram histogram,
        TimeSpan elapsed) =>
        new(true, inputPath, outputs, resolved, stats, histogram, elapsed, null);

    public static ConversionResult Fail(string inputPath, string error, TimeSpan elapsed) =>
        new(false, inputPath, Array.Empty<string>(), ToneMapperKind.Auto, null, null, elapsed, error);
}
