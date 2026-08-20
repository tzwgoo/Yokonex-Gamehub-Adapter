from __future__ import annotations

import random
from functools import lru_cache
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HEIGHT = 32
WIDTH = 20


@lru_cache(maxsize=1)
def font_paths() -> tuple[Path, ...]:
    """覆盖常见无衬线、窄体和粗体数字，避免模型只记住一种字体。"""
    font_dir = Path("C:/Windows/Fonts")
    preferred = (
        "arial.ttf", "arialbd.ttf", "calibri.ttf", "calibrib.ttf", "segoeui.ttf",
        "segoeuib.ttf", "bahnschrift.ttf", "impact.ttf", "tahoma.ttf", "verdanab.ttf",
    )
    found = tuple(font_dir / name for name in preferred if (font_dir / name).exists())
    if not found:
        raise FileNotFoundError("没有找到可用于生成数字训练集的 Windows 字体")
    return found


def make_digit_sample(seed: int) -> tuple[np.ndarray, int]:
    rng = random.Random(seed)
    digit = rng.randrange(10)
    canvas = Image.new("L", (48, 56), 0)
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.truetype(str(rng.choice(font_paths())), rng.randint(27, 45))
    text = str(digit)
    box = draw.textbbox((0, 0), text, font=font, stroke_width=rng.randint(0, 1))
    width, height = box[2] - box[0], box[3] - box[1]
    x = (48 - width) // 2 - box[0] + rng.randint(-3, 3)
    y = (56 - height) // 2 - box[1] + rng.randint(-3, 3)
    draw.text((x, y), text, font=font, fill=rng.randint(205, 255), stroke_width=rng.randint(0, 1), stroke_fill=255)
    if rng.random() < 0.45:
        canvas = canvas.filter(ImageFilter.GaussianBlur(rng.uniform(0.2, 0.8)))
    image = np.asarray(canvas, dtype=np.uint8)
    if rng.random() < 0.55:
        image = cv2.resize(image, None, fx=rng.uniform(0.72, 1.25), fy=rng.uniform(0.78, 1.2), interpolation=cv2.INTER_LINEAR)
    _, image = cv2.threshold(image, rng.randint(70, 175), 255, cv2.THRESH_BINARY)
    points = cv2.findNonZero(image)
    if points is None:
        return make_digit_sample(seed + 100000)
    x, y, w, h = cv2.boundingRect(points)
    glyph = image[y:y + h, x:x + w]
    scale = min((WIDTH - 4) / max(w, 1), (HEIGHT - 4) / max(h, 1))
    resized = cv2.resize(glyph, (max(1, round(w * scale)), max(1, round(h * scale))), interpolation=cv2.INTER_AREA)
    result = np.zeros((HEIGHT, WIDTH), dtype=np.uint8)
    offset_x = (WIDTH - resized.shape[1]) // 2 + rng.randint(-1, 1)
    offset_y = (HEIGHT - resized.shape[0]) // 2 + rng.randint(-1, 1)
    offset_x = max(0, min(WIDTH - resized.shape[1], offset_x))
    offset_y = max(0, min(HEIGHT - resized.shape[0], offset_y))
    result[offset_y:offset_y + resized.shape[0], offset_x:offset_x + resized.shape[1]] = resized
    noise = np.random.default_rng(seed).normal(0, rng.uniform(0, 10), result.shape)
    return np.clip(result.astype(np.float32) + noise, 0, 255).astype(np.uint8), digit

