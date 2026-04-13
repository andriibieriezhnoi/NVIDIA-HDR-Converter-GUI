using CommunityToolkit.Mvvm.ComponentModel;

namespace NHC.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "NVIDIA HDR Converter";
}
