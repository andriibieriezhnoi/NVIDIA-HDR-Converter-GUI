using CommunityToolkit.Mvvm.ComponentModel;
using NHC.Core.Abstractions;

namespace NHC.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel(IColorEnhancer enhancer)
    {
        ActiveExecutionProvider = enhancer.IsAvailable ? enhancer.ActiveExecutionProvider : "unavailable";
        EnhancementAvailable = enhancer.IsAvailable;
    }

    [ObservableProperty] private string _activeExecutionProvider = "unknown";
    [ObservableProperty] private bool _enhancementAvailable;
}
