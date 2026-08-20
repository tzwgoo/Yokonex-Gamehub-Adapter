from __future__ import annotations

import argparse
import copy
import random
from pathlib import Path

import numpy as np
import torch
from torch import nn
from torch.utils.data import DataLoader, Dataset

from synthetic_digits import HEIGHT, WIDTH, make_digit_sample


class DigitNet(nn.Module):
    """小型数字分类器，运行时只依赖 OpenCV DNN。"""

    def __init__(self) -> None:
        super().__init__()
        self.features = nn.Sequential(
            nn.Conv2d(1, 12, 3, 1, 1), nn.BatchNorm2d(12), nn.ReLU(), nn.MaxPool2d(2),
            nn.Conv2d(12, 24, 3, 1, 1), nn.BatchNorm2d(24), nn.ReLU(), nn.MaxPool2d(2),
            nn.Conv2d(24, 32, 3, 1, 1), nn.ReLU(),
        )
        self.classifier = nn.Linear(32 * 8 * 5, 10)

    def forward(self, image: torch.Tensor) -> torch.Tensor:
        return self.classifier(self.features(image).flatten(1))


class DigitDataset(Dataset):
    def __init__(self, size: int, offset: int = 0) -> None:
        self.samples = [make_digit_sample(offset + index) for index in range(size)]

    def __len__(self) -> int:
        return len(self.samples)

    def __getitem__(self, index: int) -> tuple[torch.Tensor, int]:
        image, label = self.samples[index]
        return torch.from_numpy(image[None].copy()).float().div_(255), label


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--epochs", type=int, default=10)
    parser.add_argument("--samples", type=int, default=24000)
    parser.add_argument("--output", type=Path, default=Path(__file__).parents[1] / "health_digit_model.onnx")
    args = parser.parse_args()
    random.seed(17); np.random.seed(17); torch.manual_seed(17)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model = DigitNet().to(device)
    train_loader = DataLoader(DigitDataset(args.samples), batch_size=128, shuffle=True, num_workers=0)
    validation = DataLoader(DigitDataset(2000, 100000), batch_size=256, num_workers=0)
    optimizer = torch.optim.AdamW(model.parameters(), lr=2e-3, weight_decay=1e-4)
    best_accuracy, best_state = 0.0, None
    for epoch in range(args.epochs):
        model.train(); running = 0.0
        for image, label in train_loader:
            image, label = image.to(device), label.to(device)
            loss = nn.functional.cross_entropy(model(image), label)
            optimizer.zero_grad(); loss.backward(); optimizer.step(); running += float(loss)
        model.eval(); correct = total = 0
        with torch.no_grad():
            for image, label in validation:
                prediction = model(image.to(device)).argmax(1).cpu()
                correct += int((prediction == label).sum()); total += len(label)
        accuracy = correct / total
        if accuracy > best_accuracy:
            best_accuracy, best_state = accuracy, copy.deepcopy(model.state_dict())
        print(f"epoch={epoch + 1} loss={running / len(train_loader):.4f} accuracy={accuracy:.4f}", flush=True)
    if best_state is not None:
        model.load_state_dict(best_state)
    model.eval().cpu()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    torch.onnx.export(model, torch.zeros(1, 1, HEIGHT, WIDTH), args.output,
                      input_names=["digit"], output_names=["logits"], opset_version=17)
    print(args.output)


if __name__ == "__main__":
    main()
