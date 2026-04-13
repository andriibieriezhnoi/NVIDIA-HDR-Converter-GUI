"""
One-shot exporter that turns the PyTorch color-enhancement stack from the
legacy ``NHC.py`` monolith into a single ONNX graph consumable by
``NHC.ML.ColorEnhancer``.

Two export modes:

* ``--mode distilled`` (default, recommended): exports a compact
  UNet-style color-delta network that the WinUI app ships as the default
  asset. Keeps the MSIX < 150 MB.

* ``--mode ensemble``: exports the full VGG16 + ResNet34 + DenseNet121 + CBAM
  stack as-is. Useful for parity testing against the legacy Python app,
  but the resulting ONNX is hundreds of megabytes and requires tiling at
  inference.

The exporter intentionally imports the model classes from the legacy file
by path rather than rewriting them here — the source-of-truth for the v1
ML pipeline remains ``NHC.py`` until the WinUI app reaches parity.
"""

from __future__ import annotations

import argparse
import importlib.util
import logging
import sys
from pathlib import Path

import torch
import torch.nn as nn


LOG = logging.getLogger("nhc.onnx-export")


def _load_legacy_module(legacy_path: Path):
    """Load NHC.py as a module so we can reuse ColorCorrectionNet."""
    spec = importlib.util.spec_from_file_location("legacy_nhc", legacy_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load legacy module from {legacy_path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules["legacy_nhc"] = module
    spec.loader.exec_module(module)
    return module


class DistilledColorDeltaNet(nn.Module):
    """
    Small UNet-like network that learns a color delta on top of a tone-mapped
    image. Intentionally shallow: 4 depth levels, 32 base channels. Trained
    separately via knowledge distillation against the ensemble teacher.

    We define it here rather than in NHC.py because it's new to the WinUI
    rewrite and has no reason to exist in the legacy codebase.
    """

    def __init__(self, base: int = 32):
        super().__init__()
        self.enc1 = self._block(3, base)
        self.enc2 = self._block(base, base * 2)
        self.enc3 = self._block(base * 2, base * 4)
        self.bottleneck = self._block(base * 4, base * 8)
        self.up3 = nn.ConvTranspose2d(base * 8, base * 4, 2, 2)
        self.dec3 = self._block(base * 8, base * 4)
        self.up2 = nn.ConvTranspose2d(base * 4, base * 2, 2, 2)
        self.dec2 = self._block(base * 4, base * 2)
        self.up1 = nn.ConvTranspose2d(base * 2, base, 2, 2)
        self.dec1 = self._block(base * 2, base)
        self.head = nn.Conv2d(base, 3, 1)
        self.pool = nn.AvgPool2d(2)

    @staticmethod
    def _block(in_ch: int, out_ch: int) -> nn.Sequential:
        return nn.Sequential(
            nn.Conv2d(in_ch, out_ch, 3, padding=1),
            nn.GELU(),
            nn.Conv2d(out_ch, out_ch, 3, padding=1),
            nn.GELU(),
        )

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        e1 = self.enc1(x)
        e2 = self.enc2(self.pool(e1))
        e3 = self.enc3(self.pool(e2))
        b = self.bottleneck(self.pool(e3))
        d3 = self.dec3(torch.cat([self.up3(b), e3], dim=1))
        d2 = self.dec2(torch.cat([self.up2(d3), e2], dim=1))
        d1 = self.dec1(torch.cat([self.up1(d2), e1], dim=1))
        # Residual delta, clamped to ±0.1 to match the legacy enhancer envelope.
        delta = self.head(d1).tanh() * 0.1
        return (x + delta).clamp(0.0, 1.0)


def _export(model: nn.Module, output: Path, opset: int = 17) -> None:
    model.eval()
    dummy = torch.randn(1, 3, 256, 256)
    output.parent.mkdir(parents=True, exist_ok=True)
    torch.onnx.export(
        model,
        dummy,
        output.as_posix(),
        input_names=["input"],
        output_names=["output"],
        dynamic_axes={"input": {0: "N", 2: "H", 3: "W"}, "output": {0: "N", 2: "H", 3: "W"}},
        opset_version=opset,
        do_constant_folding=True,
    )
    LOG.info("Wrote %s (%.1f MB)", output, output.stat().st_size / 1_048_576)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=["distilled", "ensemble"], default="distilled")
    parser.add_argument("--legacy", type=Path, default=Path(__file__).resolve().parents[2] / "NHC.py")
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[2]
                        / "winui" / "assets" / "models" / "enhancer.onnx")
    parser.add_argument("--opset", type=int, default=17)
    args = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")

    if args.mode == "distilled":
        model = DistilledColorDeltaNet()
        LOG.warning(
            "Exporting the *untrained* distilled net. Replace this step with a "
            "trained checkpoint before shipping.")
    else:
        legacy = _load_legacy_module(args.legacy)
        ColorCorrectionNet = getattr(legacy, "ColorCorrectionNet", None)
        if ColorCorrectionNet is None:
            LOG.error("ColorCorrectionNet not found in %s", args.legacy)
            return 2
        model = ColorCorrectionNet()

    _export(model, args.output, opset=args.opset)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
