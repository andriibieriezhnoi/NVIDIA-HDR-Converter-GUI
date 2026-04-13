using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using NHC.App.Services;
using NHC.App.ViewModels;
using NHC.Core.Abstractions;
using NHC.Core.Pipeline;
using NHC.Imaging.Windows;
using NHC.ML;
using Serilog;

namespace NHC.App;

/// <summary>
/// Application entry point. Builds the generic host, wires services and
/// view-models via DI, and creates the main window.
/// </summary>
public partial class App : Application
{
    public static IHost Host { get; private set; } = default!;

    private Window? _window;

    public App()
    {
        InitializeComponent();
        Host = BuildHost();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = Host.Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }

    private static IHost BuildHost()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Debug()
            .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "nhc-app.log"),
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((ctx, services) =>
            {
                // Platform services
                services.AddSingleton<IHdrDecoder, JxrDecoder>();
                services.AddSingleton<ISdrEncoder, WicJpegEncoder>();
                services.AddSingleton<IHdrEncoder, WicTiffEncoder>();

                services.AddSingleton(sp =>
                {
                    var modelPath = Path.Combine(AppContext.BaseDirectory, "Assets", "models", "enhancer.onnx");
                    return new OrtEnvironment(modelPath, sp.GetRequiredService<ILogger<OrtEnvironment>>());
                });
                services.AddSingleton<IColorEnhancer, ColorEnhancer>();

                services.AddSingleton<ConversionPipeline>();

                // UI services
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<IUiDispatcher, DispatcherQueueAdapter>();

                // View-models
                services.AddTransient<ConversionViewModel>();
                services.AddTransient<BatchViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<MainViewModel>();

                // Windows
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }
}
