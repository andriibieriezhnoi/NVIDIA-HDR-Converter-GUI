# NVIDIA HDR Converter

Native Windows app that converts NVIDIA HDR screenshots (`.jxr` from Ansel
or the Xbox Game Bar) to **JPEG** or **16-bit TIFF**, with intelligent
tone mapping and optional AI color enhancement.

Built on **.NET 8 / WinUI 3 / MVVM**. ML runs via **ONNX Runtime** with
a CUDA execution provider and CPU fallback — no Python at runtime. JXR
decoding goes through Windows Imaging Component directly, so there are
no third-party codec DLLs to ship.

## Features

- **JXR (JPEG XR) decode** at full scRGB float32 precision via WIC.
- **Tone mapping** — ACES (Narkowicz fit), Hable (Uncharted 2), extended
  Reinhard with white point. An **Auto** selector picks the best
  operator from simple image statistics (dynamic range, shadow/highlight
  fraction, mean luminance).
- **AI color enhancement** — compact UNet color-delta network, tiled at
  512 px for 4K safety. Blends into the tone-mapped output by a
  user-controlled strength.
- **Edge enhancement** — Sobel with an amplitude cap at 20% of local
  luminance to avoid haloing.
- **Bayer 8×8 ordered dither** before 8-bit quantisation kills banding
  in flat gradients.
- **Batch mode** — folder processing with cancellation and streaming
  progress.
- **CLI** — `nhc --in x.jxr --out x.jpg --tone auto` for scripted runs.

## Repository layout

```
.
├── NHC.sln
├── Directory.Build.props                 # shared MSBuild (Nullable, warnings-as-errors)
├── src/
│   ├── NHC.Core/                         # .NET 8, cross-platform — testable on any OS
│   ├── NHC.Imaging.Windows/              # net8.0-windows, WIC JXR / JPEG / TIFF
│   ├── NHC.ML/                           # ONNX Runtime session + tiled enhancer
│   ├── NHC.Cli/                          # headless `nhc` converter
│   └── NHC.App/                          # WinUI 3 MSIX shell
├── tests/NHC.Core.Tests/                 # xUnit + FluentAssertions
├── assets/models/                        # enhancer.onnx lives here at runtime
└── tools/onnx-export/                    # PyTorch → ONNX exporter
```

## Build

Prerequisites on Windows 10 19041+:

- Visual Studio 2022 17.10+ with the **.NET Desktop** and **Windows App
  SDK C#** workloads, or
- the .NET 8 SDK + Windows App SDK build tooling (`dotnet workload
  install microsoft-windowsappsdk`).

```powershell
dotnet restore NHC.sln
dotnet build   NHC.sln -c Release
dotnet test    tests/NHC.Core.Tests/NHC.Core.Tests.csproj
```

Core library + tests also build on Linux and macOS (they have no Windows
dependency), which is what the GitHub Actions workflow exercises on
every push.

## Run

Launch from Visual Studio (set `NHC.App` as startup) or produce an MSIX:

```powershell
dotnet publish src/NHC.App/NHC.App.csproj `
  -c Release -r win-x64 --self-contained true `
  /p:GenerateAppxPackageOnBuild=true
```

Or run the CLI:

```powershell
dotnet run --project src/NHC.Cli -- --in shot.jxr --out shot.jpg --tone auto
```

## ONNX model asset

The WinUI app loads `assets/models/enhancer.onnx` at startup. To produce
it, install the exporter's dependencies and run the script:

```bash
pip install -r tools/onnx-export/requirements.txt
python tools/onnx-export/export.py --checkpoint path/to/trained.pt
```

Running without `--checkpoint` writes an untrained graph that is only
useful for smoke-testing the ORT session wiring.

## Architecture notes

- **MVVM** via `CommunityToolkit.Mvvm` source generators. View-models
  depend on `IDialogService` / `IUiDispatcher` abstractions so they are
  unit-testable without WinUI.
- **DI / hosting** via `Microsoft.Extensions.Hosting`; the same host
  graph is used by `NHC.App` and `NHC.Cli`.
- **Progress** streams out of `ConversionPipeline` as
  `IAsyncEnumerable<ProgressEvent>` on top of
  `System.Threading.Channels`, so the UI can `await foreach` without
  reasoning about threads.
- **Auto tone mapper** is a three-branch rule on `ImageStatistics`
  rather than the hand-crafted scoring tree from the previous
  implementation — far easier to test and reason about.
- **Logging** is Serilog, writing to `%LOCALAPPDATA%\…\nhc-app.log`.

## License

See [`LICENSE`](LICENSE).
