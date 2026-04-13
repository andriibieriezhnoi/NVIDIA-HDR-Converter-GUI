using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace NHC.App.Services;

/// <summary>File / folder picker abstractions so view-models stay free of WinUI types.</summary>
public interface IDialogService
{
    Task<StorageFile?> PickJxrFileAsync();

    Task<StorageFile?> PickOutputFileAsync(string suggestedName, string extension);

    Task<StorageFolder?> PickFolderAsync();

    Task ShowErrorAsync(string title, string message);
}

public sealed class DialogService : IDialogService
{
    public Window? HostWindow { get; set; }

    public async Task<StorageFile?> PickJxrFileAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".jxr");
        InitializeWithWindow(picker);
        return await picker.PickSingleFileAsync();
    }

    public async Task<StorageFile?> PickOutputFileAsync(string suggestedName, string extension)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedName,
        };
        picker.FileTypeChoices.Add("Image", new List<string> { extension });
        InitializeWithWindow(picker);
        return await picker.PickSaveFileAsync();
    }

    public async Task<StorageFolder?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow(picker);
        return await picker.PickSingleFolderAsync();
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        if (HostWindow is null) return;
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = HostWindow.Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private void InitializeWithWindow(object target)
    {
        if (HostWindow is null) return;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(HostWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(target, hwnd);
    }
}
