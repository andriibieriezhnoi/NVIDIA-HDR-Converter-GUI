# ONNX export

One-shot tool that exports the PyTorch color-enhancement topology into
`enhancer.onnx`, which `NHC.ML.ColorEnhancer` loads at runtime.

```bash
pip install -r requirements.txt
python export.py                              # untrained; smoke-test only
python export.py --checkpoint trained.pt      # production
```

Output path defaults to `assets/models/enhancer.onnx` at the repo root,
which is where the WinUI app looks on startup.
