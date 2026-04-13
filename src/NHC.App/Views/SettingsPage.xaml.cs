using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NHC.App.ViewModels;

namespace NHC.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }
}
