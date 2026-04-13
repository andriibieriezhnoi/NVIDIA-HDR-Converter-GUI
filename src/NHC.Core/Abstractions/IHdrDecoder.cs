using NHC.Core.Imaging;

namespace NHC.Core.Abstractions;

/// <summary>
/// Reads an HDR image from a file path or stream. Implementations live in
/// <c>NHC.Imaging.Windows</c> (WIC/DirectXTex) so <c>NHC.Core</c> stays
/// platform-agnostic and unit-testable on any OS.
/// </summary>
public interface IHdrDecoder
{
    ValueTask<HdrImage> DecodeAsync(string filePath, CancellationToken ct = default);
}
