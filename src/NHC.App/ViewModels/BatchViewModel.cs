using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NHC.App.Services;
using NHC.Core.Pipeline;
using NHC.Core.ToneMapping;

namespace NHC.App.ViewModels;

/// <summary>View-model for the folder-batch page. Streams progress via IAsyncEnumerable.</summary>
public partial class BatchViewModel : ObservableObject
{
    private readonly ConversionPipeline _pipeline;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _ui;

    public BatchViewModel(ConversionPipeline pipeline, IDialogService dialogs, IUiDispatcher ui)
    {
        _pipeline = pipeline;
        _dialogs = dialogs;
        _ui = ui;
    }

    public ObservableCollection<BatchItem> Items { get; } = new();

    [ObservableProperty] private string? _inputFolder;
    [ObservableProperty] private string? _outputFolder;
    [ObservableProperty] private ToneMapperKind _toneMapper = ToneMapperKind.Auto;
    [ObservableProperty] private float _progress;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Ready.";

    [RelayCommand]
    private async Task PickInputFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync();
        if (folder is null) return;
        InputFolder = folder.Path;
        Items.Clear();
        foreach (var file in Directory.EnumerateFiles(folder.Path, "*.jxr", SearchOption.TopDirectoryOnly))
        {
            Items.Add(new BatchItem(file));
        }
    }

    [RelayCommand]
    private async Task PickOutputFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync();
        if (folder is not null) OutputFolder = folder.Path;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync(CancellationToken ct)
    {
        if (Items.Count == 0 || string.IsNullOrEmpty(OutputFolder)) return;
        IsBusy = true;
        Progress = 0;
        try
        {
            var requests = Items.Select(i => new ConversionRequest(
                InputPath: i.InputPath,
                OutputPath: Path.Combine(OutputFolder!, Path.ChangeExtension(Path.GetFileName(i.InputPath), ".jpg")),
                Format: OutputFormat.Jpeg,
                ToneMapper: ToneMapper,
                ToneMapperSettings: ToneMapperSettings.Default,
                EnableEnhancement: false,
                EnhancementStrength: 0.5f,
                EdgeStrength: 0f)).ToList();

            await foreach (var evt in _pipeline.ConvertBatchAsync(requests, ct).ConfigureAwait(true))
            {
                var captured = evt;
                _ui.Enqueue(() =>
                {
                    if (captured.TotalCount > 0)
                    {
                        Progress = (captured.CurrentIndex + captured.StageFraction) / captured.TotalCount;
                    }
                    var item = Items.FirstOrDefault(i => i.InputPath == captured.FilePath);
                    if (item is not null)
                    {
                        item.Stage = captured.Stage.ToString();
                    }
                    StatusMessage = $"{captured.Stage}: {Path.GetFileName(captured.FilePath)}";
                });
            }

            StatusMessage = $"Batch completed — {Items.Count} files.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRun() => !IsBusy && Items.Count > 0 && !string.IsNullOrEmpty(OutputFolder);

    partial void OnInputFolderChanged(string? value) => RunCommand.NotifyCanExecuteChanged();
    partial void OnOutputFolderChanged(string? value) => RunCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => RunCommand.NotifyCanExecuteChanged();
}

public partial class BatchItem : ObservableObject
{
    public BatchItem(string inputPath)
    {
        InputPath = inputPath;
        FileName = Path.GetFileName(inputPath);
    }

    public string InputPath { get; }
    public string FileName { get; }
    [ObservableProperty] private string _stage = "Queued";
}
