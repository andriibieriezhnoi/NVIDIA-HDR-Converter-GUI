namespace NHC.Core.Pipeline;

/// <summary>
/// Emitted by the pipeline as a conversion progresses. Surfaced over an
/// <see cref="IAsyncEnumerable{ProgressEvent}"/> so the UI's ViewModel can
/// consume them with <c>await foreach</c> without caring about threading.
/// </summary>
public readonly record struct ProgressEvent(
    ProgressStage Stage,
    string FilePath,
    int CurrentIndex,
    int TotalCount,
    float StageFraction,
    string? Message);

public enum ProgressStage
{
    Queued,
    Decoding,
    Analysing,
    ToneMapping,
    Enhancing,
    Encoding,
    Completed,
    Failed,
}
