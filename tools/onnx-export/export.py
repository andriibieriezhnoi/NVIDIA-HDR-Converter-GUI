"""
One-shot exporter that produces the ONNX model consumed by
``NHC.ML.ColorEnhancer`` at runtime.

The shipped network is a compact UNet-style color-delta net — 3 encoder
levels, a bottleneck, and 3 decoder levels, at 32 base channels. It
predicts a residual in RGB clamped to ±0.1 and blends back onto the input.
The small footprint keeps the MSIX asset under 20 MB.

This script exports the *topology* only. Swap in a trained checkpoint
before shipping:

    python export.py --checkpoint path/to/trained.pt

Without ``--checkpoint`` the exporter writes an untrained network and
logs a warning — useful to smoke-test the ORT session wiring end-to-end.
"""

from __future__ import annotations

import argparse
import logging
from pathlib import Path

import torch
import torch.nn as nn


LOG = logging.getLogger("nhc.onnx-export")


class ColorDeltaNet(nn.Module):
    """UNet-style residual color-delta network. Trained separately."""

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
        delta = self.head(d1).tanh() * 0.1
        return (x + delta).clamp(0.0, 1.0)


def _export(model: nn.Module, output: Path, opset: int) -> None:
    model.eval()
    dummy = torch.randn(1, 3, 256, 256)
    output.parent.mkdir(parents=True, exist_ok=True)
    torch.onnx.export(
        model,
        dummy,
        output.as_posix(),
        input_names=["input"],
        output_names=["output"],
        dynamic_axes={"input": {0: "N", 2: "H", 3: "W"},
                      "output": {0: "N", 2: "H", 3: "W"}},
        opset_version=opset,
        do_constant_folding=True,
    )
    LOG.info("Wrote %s (%.1f MB)", output, output.stat().st_size / 1_048_576)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--checkpoint", type=Path, default=None,
                        help="Optional .pt state-dict to load before export.")
    parser.add_argument("--output", type=Path,
                        default=Path(__file__).resolve().parents[2] / "assets" / "models" / "enhancer.onnx")
    parser.add_argument("--opset", type=int, default=17)
    args = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")

    model = ColorDeltaNet()
    if args.checkpoint is not None:
        state = torch.load(args.checkpoint, map_location="cpu")
        model.load_state_dict(state)
        LOG.info("Loaded checkpoint %s", args.checkpoint)
    else:
        LOG.warning("Exporting an *untrained* network. Pass --checkpoint before shipping.")

    _export(model, args.output, opset=args.opset)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
