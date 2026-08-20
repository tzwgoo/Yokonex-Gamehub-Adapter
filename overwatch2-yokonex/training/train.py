from __future__ import annotations

import argparse
import random
from pathlib import Path

import numpy as np
import torch
from torch import nn
from torch.utils.data import DataLoader, Dataset

from synthetic_data import HEIGHT, WIDTH, make_sample


class HealthBarNet(nn.Module):
    """小型全卷积网络：通道 0 定位完整血条，通道 1 定位已填充部分。"""

    def __init__(self) -> None:
        super().__init__()
        self.encoder = nn.Sequential(
            nn.Conv2d(3, 12, 5, 2, 2), nn.BatchNorm2d(12), nn.SiLU(),
            nn.Conv2d(12, 20, 3, 2, 1), nn.BatchNorm2d(20), nn.SiLU(),
            nn.Conv2d(20, 28, 3, 1, 1), nn.BatchNorm2d(28), nn.SiLU(),
            nn.Conv2d(28, 28, 3, 1, 1), nn.SiLU(),
        )
        self.head = nn.Sequential(nn.Conv2d(28, 20, 3, 1, 1), nn.SiLU(), nn.Conv2d(20, 2, 1))

    def forward(self, image: torch.Tensor) -> torch.Tensor:
        return self.head(self.encoder(image))


class SyntheticDataset(Dataset):
    def __init__(self, size: int, offset: int = 0, cache: bool = False) -> None:
        self.size, self.offset = size, offset
        self.cached = [make_sample(offset + index) for index in range(size)] if cache else None

    def __len__(self) -> int:
        return self.size

    def __getitem__(self, index: int) -> tuple[torch.Tensor, torch.Tensor]:
        sample = self.cached[index] if self.cached is not None else make_sample(self.offset + index)
        image = torch.from_numpy(sample.image.transpose(2, 0, 1).copy()).float().div_(255)
        mask = torch.from_numpy(sample.mask)
        mask = nn.functional.max_pool2d(mask.unsqueeze(0), 4, 4).squeeze(0)
        return image, mask


def dice_loss(logits: torch.Tensor, target: torch.Tensor) -> torch.Tensor:
    prediction = torch.sigmoid(logits)
    intersection = (prediction * target).sum((2, 3))
    return (1 - (2 * intersection + 1) / (prediction.sum((2, 3)) + target.sum((2, 3)) + 1)).mean()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--epochs", type=int, default=16)
    parser.add_argument("--samples", type=int, default=8000)
    parser.add_argument("--cache", action="store_true")
    parser.add_argument("--output", type=Path, default=Path(__file__).parents[1] / "healthbar_model.onnx")
    args = parser.parse_args()
    random.seed(7); np.random.seed(7); torch.manual_seed(7)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model = HealthBarNet().to(device)
    loader = DataLoader(SyntheticDataset(args.samples, cache=args.cache), batch_size=32, shuffle=True, num_workers=0)
    optimizer = torch.optim.AdamW(model.parameters(), lr=2e-3, weight_decay=1e-4)
    positive_weight = torch.tensor([18.0, 22.0], device=device).view(1, 2, 1, 1)
    for epoch in range(args.epochs):
        model.train(); running = 0.0
        for image, target in loader:
            image, target = image.to(device), target.to(device)
            logits = model(image)
            loss = nn.functional.binary_cross_entropy_with_logits(logits, target, pos_weight=positive_weight) + dice_loss(logits, target)
            optimizer.zero_grad(); loss.backward(); optimizer.step(); running += float(loss)
        print(f"epoch={epoch + 1} loss={running / len(loader):.4f}", flush=True)
    model.eval().cpu()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    torch.onnx.export(model, torch.zeros(1, 3, HEIGHT, WIDTH), args.output, input_names=["image"], output_names=["masks"], opset_version=17)
    print(args.output)


if __name__ == "__main__":
    main()
