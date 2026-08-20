# 通用血条与数字血量模型训练

模型是双通道轻量分割网络：第一通道定位完整血条，第二通道定位已填充部分。运行时使用 OpenCV DNN，不要求显卡。

训练数据由 `training/synthetic_data.py` 实时生成，覆盖纯色、渐变、分段、护盾、受伤残影、不同颜色和复杂背景。网上可见的游戏截图仅用于确认 HUD 区域和常见误识别场景，没有复制进仓库或模型包。

开发环境需要 PyTorch、OpenCV、ONNX 和 NumPy：

```powershell
python training/train.py --epochs 12 --samples 1200 --cache
```

模型固定接收 512×288 图像，因此 1080p、2K 和 4K 会走同一套归一化坐标。新增游戏时通常只需调整 `vision.json` 的 `playerRoi` 和 `anchor`。

数字血量使用独立的轻量分类模型。训练样本由 Windows 常见字体合成，包含字号、宽高、模糊、描边、位移和噪声变化：

```powershell
python training/train_digits.py --epochs 10 --samples 24000
```

运行时先在 `numericRoi` 内分割字符，再逐位识别 1～4 位整数。数字区域使用归一化坐标，因此三种目标分辨率共用一个模型。
