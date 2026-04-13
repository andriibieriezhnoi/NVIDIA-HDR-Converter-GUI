# NVIDIA HDR Converter — WinUI 3 rewrite

Native Windows rewrite of the Python/Tkinter `NHC.py` monolith. Targets
**C# / .NET 8 / WinUI 3** with MVVM, and runs the AI color-enhancement
stage via **ONNX Runtime** (CUDA EP + CPU fallback) so there is no Python
at runtime.

## Why rewrite?

The legacy `NHC.py` bundled UI, PyTorch inference, tone-mapping, JXR
decoding, file I/O and threading into one 3,400-line `App` god-class. It
shipped as a ~500 MB PyInstaller EXE, had zero tests, magic constants
scattered across a hand-crafted scoring tree, several buggy algorithm
stubs (JXR metadata "extraction", `tone_map_filmic`, disabled perceptual
color preservation) and a Tkinter UI that stuttered on 4K previews.
The rewrite addresses those problems structurally — see
[`../CLAUDE.md`-style plan](../winui/README.md) for the architectural
rationale.

## Solution layout

```
winui/
├── NHC.sln
├── Directory.Build.props                 # shared MSBuild settings (Nullable, warnings as errors)
├── src/
│   ├── NHC.Core/                         # .NET 8, no Windows deps — testable on any OS
│   │   ├── Imaging/      HdrImage, SdrImage, HdrMetadata, Srgb OETF
│   │   ├── ToneMapping/  IToneMapper + ACES, Hable, Reinhard-Extended, AutoSelector
│   │   ├── Analysis/     ImageStatistics, Histogram, Sobel, BayerDither
│   │   ├── Abstractions/ IHdrDecoder, ISdrEncoder, IHdrEncoder, IColorEnhancer
│   │   └── Pipeline/     ConversionPipeline + ConversionRequest/Result + ProgressEvent
│   ├── NHC.Imaging.Windows/              # net8.0-windows, WIC JXR + JPEG/TIFF encoders
│   ├── NHC.ML/                           # ONNX Runtime session pool, tiled enhancer
│   ├── NHC.Cli/                          # headless `nhc` converter
│   └── NHC.App/                          # WinUI 3 MVVM shell (MSIX)
├── tests/NHC.Core.Tests/                 # xUnit + FluentAssertions
└── assets/models/                        # enhancer.onnx (produced by tools/onnx-export)
```

## Architectural decisions

| Concern | Choice | Rationale |
|---|---|---|
| UI | WinUI 3 + CommunityToolkit.Mvvm | Native Fluent look, source-generated MVVM, MSIX packaging |
| DI / hosting | `Microsoft.Extensions.Hosting` + Serilog | One host graph shared by CLI and UI |
| HDR decoding | WIC via `BitmapDecoder` (JPEG XR codec ID) | Native, no extra deps; half-float pixel format preserves scRGB |
| SDR/HDR encoding | WIC JPEG + 16-bit TIFF | Same surface as decoder |
| Tone mapping | Pure C# `Parallel.For` per row | No GPU dependency, ~30 ms for 4K on a modern CPU |
| Auto tone selection | Simple three-branch rule on `ImageStatistics` | Replaces the 100+ line scoring tree in NHC.py |
| AI enhancement | ONNX Runtime 1.20 + CUDA EP with CPU fallback | One graph, tiled at 512 px to survive 8 GB GPUs |
| Batch progress | `System.Threading.Channels` → `IAsyncEnumerable<ProgressEvent>` | Back-pressure safe, UI binds with `await foreach` |
| Logging | Serilog to `%LOCALAPPDATA%\…\nhc-app.log` | Structured, file + debug sinks |

## Build

```powershell
# On Windows 10 19041+ with Visual Studio 2022 (Desktop + .NET 8 + WinUI workloads):
dotnet restore winui/NHC.sln
dotnet build winui/NHC.sln -c Release
dotnet test  winui/tests/NHC.Core.Tests/NHC.Core.Tests.csproj
```

To produce an MSIX:

```powershell
dotnet publish winui/src/NHC.App/NHC.App.csproj `
  -c Release -r win-x64 --self-contained true `
  /p:GenerateAppxPackageOnBuild=true
```

## Status (alpha — Phase 1–4 landed, Phase 5+ in progress)

| Phase | Deliverable | State |
|---|---|---|
| 1 | `NHC.Core` imaging primitives + sRGB OETF | done |
| 2 | Tone mappers (ACES/Hable/Reinhard-Extended) + Auto selector + Sobel + Bayer dither + tests | done |
| 3 | `ConversionPipeline` + `NHC.Cli` + batch progress events | done |
| 4 | ONNX export script + `NHC.ML` session pool + tiled enhancer | scaffolded; model training pending |
| 5 | WinUI 3 shell (Nav / Convert / Batch / Settings pages, MVVM) | scaffolded; Win2D preview pending |
| 6 | Batch UI + cancellation + settings persistence | partial |
| 7 | Wire auto-selector into UI + live preview | pending |
| 8 | MSIX packaging + signing + optional model package / first-run download | pending |
| 9 | Perf pass + telemetry | pending |

The legacy `NHC.py` app in the repo root still works and remains the
reference implementation until parity tests pass.
