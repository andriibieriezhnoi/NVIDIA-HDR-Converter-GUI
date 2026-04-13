using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;

namespace NHC.ML;

/// <summary>
/// Builds a single cached <see cref="InferenceSession"/> using the best
/// available execution provider. Probes CUDA first, falls back to CPU.
/// </summary>
public sealed class OrtEnvironment : IDisposable
{
    private readonly ILogger<OrtEnvironment> _log;
    private readonly string _modelPath;
    private InferenceSession? _session;
    private string _activeProvider = "none";

    public OrtEnvironment(string modelPath, ILogger<OrtEnvironment>? log = null)
    {
        _modelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
        _log = log ?? NullLogger<OrtEnvironment>.Instance;
    }

    public string ActiveProvider => _activeProvider;

    public InferenceSession Session => _session ?? throw new InvalidOperationException(
        "OrtEnvironment not initialised. Call LoadAsync first.");

    public async Task<bool> LoadAsync(CancellationToken ct = default)
    {
        if (_session is not null) return true;
        if (!File.Exists(_modelPath))
        {
            _log.LogWarning("ONNX model not found at {Path}; enhancement disabled.", _modelPath);
            return false;
        }

        await Task.Run(() =>
        {
            var opts = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            };

            try
            {
                opts.AppendExecutionProvider_CUDA(deviceId: 0);
                _session = new InferenceSession(_modelPath, opts);
                _activeProvider = "CUDA";
                _log.LogInformation("ONNX Runtime initialised with CUDA EP.");
                return;
            }
            catch (Exception ex)
            {
                _log.LogInformation(ex, "CUDA EP unavailable; falling back to CPU.");
            }

            opts = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            };
            _session = new InferenceSession(_modelPath, opts);
            _activeProvider = "CPU";
            _log.LogInformation("ONNX Runtime initialised with CPU EP.");
        }, ct).ConfigureAwait(false);

        return _session is not null;
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}
