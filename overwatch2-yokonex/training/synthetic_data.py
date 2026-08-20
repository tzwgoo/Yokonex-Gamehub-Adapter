from __future__ import annotations

import random
from dataclasses import dataclass

import cv2
import numpy as np


WIDTH, HEIGHT = 512, 288


@dataclass(frozen=True)
class Sample:
    image: np.ndarray
    mask: np.ndarray
    bars: list[tuple[int, int, int, int, float]]


def _background(rng: random.Random) -> np.ndarray:
    """生成接近游戏画面的复杂背景，避免模型只记住纯色底。"""
    color_a = np.array([rng.randrange(15, 180) for _ in range(3)], dtype=np.float32)
    color_b = np.array([rng.randrange(15, 180) for _ in range(3)], dtype=np.float32)
    ramp = np.linspace(0, 1, WIDTH, dtype=np.float32)[None, :, None]
    image = np.repeat(color_a[None, None, :] * (1 - ramp) + color_b[None, None, :] * ramp, HEIGHT, axis=0)
    image += np.random.default_rng(rng.randrange(2**32)).normal(0, rng.uniform(5, 22), image.shape)
    image = np.clip(image, 0, 255).astype(np.uint8)
    for _ in range(rng.randrange(8, 28)):
        x1, y1 = rng.randrange(WIDTH), rng.randrange(HEIGHT)
        x2, y2 = rng.randrange(WIDTH), rng.randrange(HEIGHT)
        cv2.line(image, (x1, y1), (x2, y2), tuple(rng.randrange(256) for _ in range(3)), rng.randrange(1, 5))
    return image


def make_sample(seed: int, *, bars_count: int | None = None) -> Sample:
    rng = random.Random(seed)
    image = _background(rng)
    mask = np.zeros((2, HEIGHT, WIDTH), dtype=np.float32)
    bars: list[tuple[int, int, int, int, float]] = []
    count = rng.randrange(0, 5) if bars_count is None else bars_count
    for _ in range(count):
        width = rng.randrange(70, 330)
        height = rng.randrange(7, 27)
        x = rng.randrange(8, WIDTH - width - 8)
        y = rng.randrange(8, HEIGHT - height - 8)
        ratio = rng.random()
        border = rng.randrange(1, 4)
        background = tuple(rng.randrange(5, 65) for _ in range(3))
        fill = tuple(rng.randrange(60, 256) for _ in range(3))

        cv2.rectangle(image, (x - border, y - border), (x + width + border, y + height + border), (230, 230, 230), border)
        cv2.rectangle(image, (x, y), (x + width, y + height), background, -1)
        filled = max(0, min(width, round(width * ratio)))
        if filled:
            cv2.rectangle(image, (x, y), (x + filled, y + height), fill, -1)
            # 渐变、分段和受伤残影都在同一标注协议下生成。
            if rng.random() < 0.45:
                for column in range(filled):
                    factor = 0.72 + 0.28 * column / max(filled - 1, 1)
                    color = tuple(int(channel * factor) for channel in fill)
                    cv2.line(image, (x + column, y), (x + column, y + height), color, 1)
            if rng.random() < 0.5:
                segment = rng.randrange(16, 45)
                for sx in range(x + segment, x + width, segment):
                    cv2.line(image, (sx, y), (sx, y + height), background, rng.randrange(1, 3))
            if rng.random() < 0.3 and filled < width - 4:
                trail = min(width, filled + rng.randrange(4, max(5, width // 4)))
                cv2.rectangle(image, (x + filled, y), (x + trail, y + height), (90, 120, 190), -1)

        cv2.rectangle(mask[0], (x, y), (x + width, y + height), 1.0, -1)
        if filled:
            cv2.rectangle(mask[1], (x, y), (x + filled, y + height), 1.0, -1)
        bars.append((x, y, width, height, ratio))

    # 加入容易被误认成血条的普通 UI 线段，但不写入标签。
    for _ in range(rng.randrange(2, 10)):
        x, y = rng.randrange(WIDTH - 50), rng.randrange(HEIGHT)
        w = rng.randrange(20, min(260, WIDTH - x))
        cv2.line(image, (x, y), (x + w, y), tuple(rng.randrange(256) for _ in range(3)), rng.randrange(1, 4))
    return Sample(image=cv2.cvtColor(image, cv2.COLOR_BGR2RGB), mask=mask, bars=bars)
