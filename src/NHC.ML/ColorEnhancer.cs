using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NHC.Core.Abstractions;
using NHC.Core.Imaging;

namespace NHC.ML;

/// <summary>
/// Runs the distilled color-enhancement ONNX model and blends its output with
/// the tone-mapped input by <c>strength</c>. Tiles the image at 512 px so 4K
/// inputs don't OOM mid-size GPUs.
/// </summary>
public sealed class ColorEnhancer : IColorEnhancer
{
    private const int TileSize = 512;
    private const int TileOverlap = 16;

    private readonly OrtEnvironment _env;
    private readonly ILogger<ColorEnhancer> _log;
    private readonly string _inputName;
    private readonly string _outputName;

    public ColorEnhancer(OrtEnvironment env, ILogger<ColorEnhancer>? log = null)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _log = log ?? NullLogger<ColorEnhancer>.Instance;
        _inputName = env.Session.InputMetadata.Keys.First();
        _outputName = env.Session.OutputMetadata.Keys.First();
    }

    public bool IsAvailable => true;

    public string ActiveExecutionProvider => _env.ActiveProvider;

    public ValueTask<HdrImage> EnhanceAsync(HdrImage toneMapped, float strength, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(toneMapped);
        strength = Math.Clamp(strength, 0f, 1f);
        if (strength <= 0f) return new ValueTask<HdrImage>(toneMapped);

        // Run synchronously on a worker thread to avoid blocking the pipeline's IO thread.
        return new ValueTask<HdrImage>(Task.Run(() => EnhanceCore(toneMapped, strength, ct), ct));
    }

    private HdrImage EnhanceCore(HdrImage input, float strength, CancellationToken ct)
    {
        var w = input.Width;
        var h = input.Height;
        var output = input.CloneShape();
        Array.Copy(input.Pixels, output.Pixels, input.Pixels.Length);

        for (var ty = 0; ty < h; ty += TileSize - TileOverlap)
        {
            for (var tx = 0; tx < w; tx += TileSize - TileOverlap)
            {
                ct.ThrowIfCancellationRequested();
                var tileW = Math.Min(TileSize, w - tx);
                var tileH = Math.Min(TileSize, h - ty);
                if (tileW <= 0 || tileH <= 0) continue;

                var tile = ExtractTile(input, tx, ty, tileW, tileH);
                var enhanced = RunSession(tile, tileW, tileH);
                BlendTile(output, tx, ty, tileW, tileH, enhanced, strength);
            }
        }

        return output;
    }

    private float[] ExtractTile(HdrImage src, int x0, int y0, int tileW, int tileH)
    {
        var tile = new float[3 * tileW * tileH];
        for (var y = 0; y < tileH; y++)
        {
            var srcRow = ((y0 + y) * src.Width + x0) * 3;
            var dstRow = y * tileW * 3;
            Array.Copy(src.Pixels, srcRow, tile, dstRow, tileW * 3);
        }
        return tile;
    }

    private float[] RunSession(float[] tileRgb, int tileW, int tileH)
    {
        // Model expects NCHW float32 in [0,1].
        var nchw = new float[3 * tileW * tileH];
        for (int y = 0, src = 0; y < tileH; y++)
        {
            for (var x = 0; x < tileW; x++, src += 3)
            {
                var off = y * tileW + x;
                nchw[off] = tileRgb[src];                           // R plane
                nchw[tileW * tileH + off] = tileRgb[src + 1];       // G plane
                nchw[2 * tileW * tileH + off] = tileRgb[src + 2];   // B plane
            }
        }

        var tensor = new DenseTensor<float>(nchw, new[] { 1, 3, tileH, tileW });
        using var results = _env.Session.Run(new[] { NamedOnnxValue.CreateFromTensor(_inputName, tensor) });
        var outTensor = results.First(r => r.Name == _outputName).AsTensor<float>();
        var outPlanes = outTensor.ToArray();

        // NCHW → interleaved RGB.
        var interleaved = new float[3 * tileW * tileH];
        for (int y = 0, dst = 0; y < tileH; y++)
        {
            for (var x = 0; x < tileW; x++, dst += 3)
            {
                var off = y * tileW + x;
                interleaved[dst] = outPlanes[off];
                interleaved[dst + 1] = outPlanes[tileW * tileH + off];
                interleaved[dst + 2] = outPlanes[2 * tileW * tileH + off];
            }
        }

        return interleaved;
    }

    private void BlendTile(HdrImage dst, int x0, int y0, int tileW, int tileH, float[] tile, float strength)
    {
        var dstPixels = dst.Pixels;
        for (var y = 0; y < tileH; y++)
        {
            var dstRow = ((y0 + y) * dst.Width + x0) * 3;
            var srcRow = y * tileW * 3;
            for (var i = 0; i < tileW * 3; i++)
            {
                var orig = dstPixels[dstRow + i];
                var enh = tile[srcRow + i];
                dstPixels[dstRow + i] = orig + (enh - orig) * strength;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _env.Dispose();
        return ValueTask.CompletedTask;
    }
}
