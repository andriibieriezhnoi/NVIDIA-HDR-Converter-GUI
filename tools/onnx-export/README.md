# ONNX export

One-shot tool that converts the PyTorch enhancement stack into an ONNX file
consumed by `NHC.ML.ColorEnhancer` at runtime.

```bash
pip install -r requirements.txt
python export.py --mode distilled                 # small, shipped by default
python export.py --mode ensemble --output big.onnx   # full VGG+ResNet+DenseNet
```

Output path defaults to `winui/assets/models/enhancer.onnx`, which is where
the WinUI app looks at startup.
