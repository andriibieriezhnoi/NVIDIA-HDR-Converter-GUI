using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NHC.App.ViewModels;

namespace NHC.App.Views;

public sealed partial class ConversionPage : Page
{
    public ConversionViewModel ViewModel { get; }

    public ConversionPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<ConversionViewModel>();
        InitializeComponent();
    }
}
