using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NHC.App.Services;
using NHC.App.Views;

namespace NHC.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;

        // The dialog service needs an HWND for the Win32 file pickers — hand it
        // the main window now so view-models don't have to know about WinUI types.
        if (App.Host.Services.GetRequiredService<IDialogService>() is DialogService dialogs)
        {
            dialogs.HostWindow = this;
        }

        Nav.SelectedItem = Nav.MenuItems[0];
        ContentFrame.Navigate(typeof(ConversionPage));
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        var target = item.Tag switch
        {
            "convert" => typeof(ConversionPage),
            "batch" => typeof(BatchPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(ConversionPage),
        };
        ContentFrame.Navigate(target);
    }
}
