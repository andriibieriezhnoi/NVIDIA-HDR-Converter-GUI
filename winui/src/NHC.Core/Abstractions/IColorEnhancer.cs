using NHC.Core.Imaging;

namespace NHC.Core.Abstractions;

/// <summary>
/// Optional AI color enhancement stage. Produces a refined [0,1] linear image
/// from a tone-mapped input. Implementations (ONNX Runtime) live in
/// <c>NHC.ML</c>; <c>NHC.Core</c> only depends on the interface.
/// </summary>
public interface IColorEnhancer : IAsyncDisposable
{
    bool IsAvailable { get; }

    /// <summary>Name of the active ONNX Runtime execution provider (e.g. "CUDA", "CPU").</summary>
    string ActiveExecutionProvider { get; }

    /// <summary>Runs enhancement. <paramref name="strength"/> blends [0,1] between input and enhanced output.</summary>
    ValueTask<HdrImage> EnhanceAsync(HdrImage toneMapped, float strength, CancellationToken ct = default);
}
