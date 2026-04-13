using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NHC.Core.Abstractions;
using NHC.Core.Analysis;
using NHC.Core.Imaging;
using NHC.Core.ToneMapping;

namespace NHC.Core.Pipeline;

/// <summary>
/// Orchestrates decode → analyse → tone-map → enhance → encode. Emits
/// <see cref="ProgressEvent"/> values suitable for UI binding. This class is
/// deliberately platform-agnostic — it accepts decoders/encoders via DI so
/// the same pipeline runs under WinUI, the CLI, and unit tests.
/// </summary>
public sealed class ConversionPipeline
{
    private readonly IHdrDecoder _decoder;
    private readonly ISdrEncoder _jpegEncoder;
    private readonly IHdrEncoder _tiffEncoder;
    private readonly ILogger<ConversionPipeline> _log;

    public ConversionPipeline(
        IHdrDecoder decoder,
        ISdrEncoder jpegEncoder,
        IHdrEncoder tiffEncoder,
        ILogger<ConversionPipeline>? logger = null)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _jpegEncoder = jpegEncoder ?? throw new ArgumentNullException(nameof(jpegEncoder));
        _tiffEncoder = tiffEncoder ?? throw new ArgumentNullException(nameof(tiffEncoder));
        _log = logger ?? NullLogger<ConversionPipeline>.Instance;
    }

    /// <summary>Run a single request. Used by CLI and single-file UI flow.</summary>
    public async Task<ConversionResult> ConvertAsync(ConversionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sw = Stopwatch.StartNew();
        try
        {
            var (outputs, resolved, stats, histogram) = await ConvertCoreAsync(request, report: null, ct)
                .ConfigureAwait(false);
            return ConversionResult.Ok(request.InputPath, outputs, resolved, stats, histogram, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Conversion failed for {Path}", request.InputPath);
            return ConversionResult.Fail(request.InputPath, ex.Message, sw.Elapsed);
        }
    }

    /// <summary>Run a batch, streaming progress. Used by the batch UI flow.</summary>
    public async IAsyncEnumerable<ProgressEvent> ConvertBatchAsync(
        IReadOnlyList<ConversionRequest> requests,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        for (var i = 0; i < requests.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var request = requests[i];
            yield return new ProgressEvent(ProgressStage.Queued, request.InputPath, i, requests.Count, 0f, null);

            // Bridge the inner pipeline's reporter to this enumerator via a channel.
            var channel = System.Threading.Channels.Channel.CreateUnbounded<ProgressEvent>(
                new System.Threading.Channels.UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = true,
                });

            var task = Task.Run(async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    await ConvertCoreAsync(request, ev => channel.Writer.TryWrite(ev with
                    {
                        CurrentIndex = i,
                        TotalCount = requests.Count,
                    }), ct).ConfigureAwait(false);
                    channel.Writer.TryWrite(new ProgressEvent(
                        ProgressStage.Completed, request.InputPath, i, requests.Count, 1f,
                        $"Completed in {sw.Elapsed.TotalSeconds:F2}s"));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "Batch item failed for {Path}", request.InputPath);
                    channel.Writer.TryWrite(new ProgressEvent(
                        ProgressStage.Failed, request.InputPath, i, requests.Count, 1f, ex.Message));
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, ct);

            await foreach (var evt in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return evt;
            }

            await task.ConfigureAwait(false);
        }
    }

    private async Task<(IReadOnlyList<string> outputs, ToneMapperKind resolved, ImageStatistics stats, Histogram histogram)>
        ConvertCoreAsync(ConversionRequest request, Action<ProgressEvent>? report, CancellationToken ct)
    {
        Report(ProgressStage.Decoding, 0f);
        var hdr = await _decoder.DecodeAsync(request.InputPath, ct).ConfigureAwait(false);

        Report(ProgressStage.Analysing, 0.15f);
        var stats = ImageStatistics.Compute(hdr);

        var resolvedKind = request.ToneMapper == ToneMapperKind.Auto
            ? AutoToneMapSelector.Select(stats)
            : request.ToneMapper;

        Report(ProgressStage.ToneMapping, 0.25f);
        var mapper = ToneMapperFactory.Create(resolvedKind);
        var mapped = mapper.Apply(hdr, request.ToneMapperSettings);
        ct.ThrowIfCancellationRequested();

        var postEnhance = mapped;
        if (request.ClaheStrength > 0f || request.VibranceStrength > 0f)
        {
            Report(ProgressStage.Enhancing, 0.50f);
            if (request.ClaheStrength > 0f)
            {
                postEnhance = Clahe.Apply(postEnhance, request.ClaheStrength);
            }
            if (request.VibranceStrength > 0f)
            {
                postEnhance = Vibrance.Apply(postEnhance, request.VibranceStrength);
            }
            ct.ThrowIfCancellationRequested();
        }

        if (request.EdgeStrength > 0f)
        {
            postEnhance = Sobel.Enhance(postEnhance, request.EdgeStrength);
        }

        Report(ProgressStage.Encoding, 0.85f);
        var sdr = BayerDither.EncodeToSrgb(postEnhance);
        var histogram = Histogram.Compute(sdr);

        var outputs = new List<string>(2);
        if (request.Format is OutputFormat.Jpeg or OutputFormat.JpegAndTiff)
        {
            var path = Path.ChangeExtension(request.OutputPath, _jpegEncoder.FileExtension);
            await _jpegEncoder.EncodeAsync(sdr, path, ct).ConfigureAwait(false);
            outputs.Add(path);
        }

        if (request.Format is OutputFormat.Tiff or OutputFormat.JpegAndTiff)
        {
            var path = Path.ChangeExtension(request.OutputPath, _tiffEncoder.FileExtension);
            await _tiffEncoder.EncodeAsync(postEnhance, path, ct).ConfigureAwait(false);
            outputs.Add(path);
        }

        Report(ProgressStage.Completed, 1f);
        return (outputs, resolvedKind, stats, histogram);

        void Report(ProgressStage stage, float fraction) =>
            report?.Invoke(new ProgressEvent(stage, request.InputPath, 0, 1, fraction, null));
    }
}
