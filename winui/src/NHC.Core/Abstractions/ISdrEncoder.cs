using NHC.Core.Imaging;

namespace NHC.Core.Abstractions;

/// <summary>Writes an 8-bit SDR image to disk. JPEG or TIFF depending on implementation.</summary>
public interface ISdrEncoder
{
    string FileExtension { get; }

    ValueTask EncodeAsync(SdrImage image, string filePath, CancellationToken ct = default);
}

public interface IHdrEncoder
{
    string FileExtension { get; }

    /// <summary>
    /// Writes the linear float image to disk preserving as much dynamic range as
    /// the container allows (e.g. TIFF float32).
    /// </summary>
    ValueTask EncodeAsync(HdrImage image, string filePath, CancellationToken ct = default);
}
