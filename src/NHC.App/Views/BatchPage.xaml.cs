using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NHC.App.ViewModels;

namespace NHC.App.Views;

public sealed partial class BatchPage : Page
{
    public BatchViewModel ViewModel { get; }

    public BatchPage()
    {
        ViewModel = App.Host.Services.GetRequiredService<BatchViewModel>();
        InitializeComponent();
    }
}
