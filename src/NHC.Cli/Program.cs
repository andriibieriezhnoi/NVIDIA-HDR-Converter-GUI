using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NHC.Core.Abstractions;
using NHC.Core.Pipeline;
using NHC.Core.ToneMapping;
using Serilog;

namespace NHC.Cli;

/// <summary>
/// Headless converter. Shares <see cref="ConversionPipeline"/> with the WinUI app
/// so behaviour can't drift between UI and CLI.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var inputOpt = new Option<FileInfo>("--in", "Input .jxr file") { IsRequired = true };
        var outputOpt = new Option<string>("--out", "Output path (extension determines format)") { IsRequired = true };
        var toneOpt = new Option<ToneMapperKind>("--tone", () => ToneMapperKind.Auto, "Tone mapper kind");
        var exposureOpt = new Option<float>("--exposure", () => 1.0f, "Pre-tone-map exposure multiplier");
        var gammaOpt = new Option<float>("--gamma", () => 1.0f, "Pre-tone-map gamma");
        var claheOpt = new Option<float>("--clahe", () => 0f, "CLAHE contrast-enhancement strength [0,1]");
        var vibranceOpt = new Option<float>("--vibrance", () => 0f, "Vibrance (smart saturation) strength [0,1]");
        var edgeOpt = new Option<float>("--edge", () => 0f, "Sobel edge enhancement [0,1]");

        var root = new RootCommand("NVIDIA HDR Converter — command-line")
        {
            inputOpt, outputOpt, toneOpt, exposureOpt, gammaOpt, claheOpt, vibranceOpt, edgeOpt,
        };

        root.SetHandler(async ctx =>
        {
            ctx.ExitCode = await RunAsync(
                ctx.ParseResult.GetValueForOption(inputOpt)!,
                ctx.ParseResult.GetValueForOption(outputOpt)!,
                ctx.ParseResult.GetValueForOption(toneOpt),
                ctx.ParseResult.GetValueForOption(exposureOpt),
                ctx.ParseResult.GetValueForOption(gammaOpt),
                ctx.ParseResult.GetValueForOption(claheOpt),
                ctx.ParseResult.GetValueForOption(vibranceOpt),
                ctx.ParseResult.GetValueForOption(edgeOpt),
                ctx.GetCancellationToken()).ConfigureAwait(false);
        });

        return await root.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task<int> RunAsync(
        FileInfo input,
        string output,
        ToneMapperKind tone,
        float exposure,
        float gamma,
        float clahe,
        float vibrance,
        float edge,
        CancellationToken ct)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("nhc-cli.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        using var host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        var pipeline = host.Services.GetRequiredService<ConversionPipeline>();
        var format = DetermineFormat(output);

        var request = new ConversionRequest(
            InputPath: input.FullName,
            OutputPath: output,
            Format: format,
            ToneMapper: tone,
            ToneMapperSettings: new ToneMapperSettings(exposure, gamma, 1000f),
            ClaheStrength: clahe,
            VibranceStrength: vibrance,
            EdgeStrength: edge);

        var result = await pipeline.ConvertAsync(request, ct).ConfigureAwait(false);
        if (result.Success)
        {
            Log.Information("Converted → {Outputs} in {Elapsed:F2}s (tone={Tone})",
                string.Join(", ", result.OutputPaths),
                result.Elapsed.TotalSeconds,
                result.ResolvedToneMapper);
            return 0;
        }

        Log.Error("Conversion failed: {Message}", result.ErrorMessage);
        return 1;
    }

    private static OutputFormat DetermineFormat(string output) =>
        Path.GetExtension(output).ToLowerInvariant() switch
        {
            ".tif" or ".tiff" => OutputFormat.Tiff,
            _ => OutputFormat.Jpeg,
        };

    private static void ConfigureServices(HostBuilderContext ctx, IServiceCollection services)
    {
#if WINDOWS
        services.AddSingleton<IHdrDecoder, NHC.Imaging.Windows.JxrDecoder>();
        services.AddSingleton<ISdrEncoder, NHC.Imaging.Windows.WicJpegEncoder>();
        services.AddSingleton<IHdrEncoder, NHC.Imaging.Windows.WicTiffEncoder>();
#else
        throw new PlatformNotSupportedException("NHC.Cli currently requires Windows for JXR decoding.");
#endif
        services.AddSingleton<ConversionPipeline>();
    }
}
