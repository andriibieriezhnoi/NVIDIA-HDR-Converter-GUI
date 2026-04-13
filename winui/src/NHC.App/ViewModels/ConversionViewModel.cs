using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NHC.App.Services;
using NHC.Core.Pipeline;
using NHC.Core.ToneMapping;

namespace NHC.App.ViewModels;

/// <summary>View-model for the single-file conversion page.</summary>
public partial class ConversionViewModel : ObservableObject
{
    private readonly ConversionPipeline _pipeline;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _ui;
    private readonly ILogger<ConversionViewModel> _log;

    public ConversionViewModel(
        ConversionPipeline pipeline,
        IDialogService dialogs,
        IUiDispatcher ui,
        ILogger<ConversionViewModel>? log = null)
    {
        _pipeline = pipeline;
        _dialogs = dialogs;
        _ui = ui;
        _log = log ?? NullLogger<ConversionViewModel>.Instance;
    }

    public IReadOnlyList<ToneMapperKind> ToneMappers { get; } = ToneMapperFactory.AvailableKinds;

    [ObservableProperty] private string? _inputPath;
    [ObservableProperty] private string? _outputPath;
    [ObservableProperty] private ToneMapperKind _toneMapper = ToneMapperKind.Auto;
    [ObservableProperty] private float _exposure = 1.0f;
    [ObservableProperty] private float _gamma = 1.0f;
    [ObservableProperty] private bool _enhanceEnabled;
    [ObservableProperty] private float _enhanceStrength = 0.5f;
    [ObservableProperty] private float _edgeStrength = 0.0f;
    [ObservableProperty] private float _progress;
    [ObservableProperty] private string _statusMessage = "Ready.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _resolvedToneMapper;

    [RelayCommand]
    private async Task PickInputAsync()
    {
        var file = await _dialogs.PickJxrFileAsync();
        if (file is not null) InputPath = file.Path;
    }

    [RelayCommand]
    private async Task PickOutputAsync()
    {
        var file = await _dialogs.PickOutputFileAsync("converted", ".jpg");
        if (file is not null) OutputPath = file.Path;
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(InputPath) || string.IsNullOrEmpty(OutputPath)) return;
        IsBusy = true;
        Progress = 0f;
        StatusMessage = "Converting…";
        try
        {
            var request = new ConversionRequest(
                InputPath: InputPath,
                OutputPath: OutputPath,
                Format: OutputFormat.Jpeg,
                ToneMapper: ToneMapper,
                ToneMapperSettings: new ToneMapperSettings(Exposure, Gamma, 1000f),
                EnableEnhancement: EnhanceEnabled,
                EnhancementStrength: EnhanceStrength,
                EdgeStrength: EdgeStrength);

            var result = await _pipeline.ConvertAsync(request, ct).ConfigureAwait(true);
            _ui.Enqueue(() =>
            {
                if (result.Success)
                {
                    Progress = 1f;
                    ResolvedToneMapper = result.ResolvedToneMapper.ToString();
                    StatusMessage = $"Converted in {result.Elapsed.TotalSeconds:F2}s (tone: {result.ResolvedToneMapper})";
                }
                else
                {
                    StatusMessage = $"Failed: {result.ErrorMessage}";
                }
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Conversion failed");
            await _dialogs.ShowErrorAsync("Conversion failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanConvert() => !IsBusy && !string.IsNullOrEmpty(InputPath) && !string.IsNullOrEmpty(OutputPath);

    partial void OnInputPathChanged(string? value) => ConvertCommand.NotifyCanExecuteChanged();
    partial void OnOutputPathChanged(string? value) => ConvertCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => ConvertCommand.NotifyCanExecuteChanged();
}
