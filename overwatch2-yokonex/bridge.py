from __future__ import annotations

import ctypes
import json
import math
import os
import sys
import time
import uuid
from collections import deque
from ctypes import wintypes
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib import request

import cv2
import numpy as np

SOURCE = "overwatch2"
COMMANDS = {
    "overwatch2.player_damaged": "overwatch2-player-damaged",
    "overwatch2.player_healed": "overwatch2-player-healed",
    "overwatch2.health_low": "overwatch2-health-low",
    "overwatch2.health_critical": "overwatch2-health-critical",
    "overwatch2.player_death": "overwatch2-player-death",
    "overwatch2.player_recovered": "overwatch2-player-recovered",
}


def build_event(source: str, event_key: str, command_id: str, data: dict[str, Any], session_id: str) -> dict[str, Any]:
    return {"source": source, "eventKey": event_key, "commandId": command_id,
            "occurredAt": datetime.now(timezone.utc).isoformat(timespec="milliseconds"),
            "eventId": str(uuid.uuid4()), "sessionId": session_id, "data": data}


class GatewayClient:
    def __init__(self) -> None:
        host = os.environ.get("YOKONEX_GATEWAY_HOST", "127.0.0.1")
        port = os.environ.get("YOKONEX_GATEWAY_PORT", "43002")
        self.endpoint = f"http://{host}:{port}/v1/events"

    def maintain(self) -> None:
        pass

    def send(self, event: dict[str, Any]) -> bool:
        payload = json.dumps(event, ensure_ascii=False, separators=(",", ":")).encode()
        try:
            with request.urlopen(request.Request(self.endpoint, data=payload, headers={"Content-Type": "application/json"}), timeout=1) as response:
                return response.status == 202
        except OSError:
            return False


def resource_path(name: str) -> Path:
    root = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
    return root / name


def load_config() -> dict[str, Any]:
    """优先读取插件目录中的外部配置，缺失时使用 EXE 内置默认值。"""
    candidates: list[Path] = []
    if os.environ.get("YOKONEX_VISION_CONFIG"):
        candidates.append(Path(os.environ["YOKONEX_VISION_CONFIG"]))
    if getattr(sys, "frozen", False):
        executable_dir = Path(sys.executable).resolve().parent
        candidates.extend((executable_dir / "vision.json", executable_dir.parent / "vision.json"))
    candidates.append(resource_path("vision.json"))
    for candidate in candidates:
        if candidate.is_file():
            return json.loads(candidate.read_text(encoding="utf-8"))
    raise FileNotFoundError("找不到 vision.json")


def sigmoid(value: np.ndarray) -> np.ndarray:
    return 1.0 / (1.0 + np.exp(-np.clip(value, -20, 20)))


class HealthBarDetector:
    """模型只定位血条与填充区域；业务层再选择玩家血条并计算百分比。"""

    def __init__(self, model_path: Path, config: dict[str, Any]) -> None:
        self.net = cv2.dnn.readNetFromONNX(str(model_path))
        self.config = config

    def detect(self, frame: np.ndarray) -> dict[str, Any] | None:
        blob = cv2.dnn.blobFromImage(frame, 1 / 255.0, (512, 288), swapRB=True)
        self.net.setInput(blob)
        output = sigmoid(self.net.forward()[0])
        bar_probability, fill_probability = output[0], output[1]
        binary = (bar_probability >= float(self.config["detectionThreshold"])).astype(np.uint8)
        count, _, stats, centroids = cv2.connectedComponentsWithStats(binary, 8)
        roi = self.config["playerRoi"]
        anchor = self.config["anchor"]
        candidates: list[tuple[float, dict[str, Any]]] = []
        height, width = binary.shape
        for index in range(1, count):
            x, y, w, h, area = (int(v) for v in stats[index])
            if area < 10 or w < 14 or h < 1 or not 2.2 <= w / max(h, 1) <= 45:
                continue
            cx, cy = float(centroids[index][0] / width), float(centroids[index][1] / height)
            if not (roi[0] <= cx <= roi[2] and roi[1] <= cy <= roi[3]):
                continue
            confidence = float(bar_probability[y:y + h, x:x + w].mean())
            if confidence < float(self.config["minimumConfidence"]):
                continue
            fill = fill_probability[y:y + h, x:x + w] >= float(self.config["fillThreshold"])
            bar = binary[y:y + h, x:x + w].astype(bool)
            ratio = 100.0 * np.count_nonzero(fill & bar) / max(np.count_nonzero(bar), 1)
            distance = math.hypot(cx - anchor[0], cy - anchor[1])
            result = {
                "healthPercent": round(max(0.0, min(100.0, ratio)), 1),
                "confidence": round(confidence, 3),
                "box": [round(x / width, 4), round(y / height, 4), round(w / width, 4), round(h / height, 4)],
            }
            candidates.append((confidence - distance * 0.35, result))
        return max(candidates, key=lambda item: item[0])[1] if candidates else None


class NumericHealthReader:
    """在配置区域内分割 1～4 位整数，再用轻量模型逐位识别。"""

    def __init__(self, model_path: Path, config: dict[str, Any]) -> None:
        self.net = cv2.dnn.readNetFromONNX(str(model_path))
        self.config = config

    @staticmethod
    def _normalize(mask: np.ndarray) -> np.ndarray:
        points = cv2.findNonZero(mask)
        if points is None:
            return np.zeros((32, 20), dtype=np.uint8)
        x, y, width, height = cv2.boundingRect(points)
        glyph = mask[y:y + height, x:x + width]
        scale = min(16 / max(width, 1), 28 / max(height, 1))
        resized = cv2.resize(glyph, (max(1, round(width * scale)), max(1, round(height * scale))), interpolation=cv2.INTER_AREA)
        result = np.zeros((32, 20), dtype=np.uint8)
        offset_x, offset_y = (20 - resized.shape[1]) // 2, (32 - resized.shape[0]) // 2
        result[offset_y:offset_y + resized.shape[0], offset_x:offset_x + resized.shape[1]] = resized
        return result

    def _recognize_mask(self, mask: np.ndarray) -> tuple[int, float] | None:
        height, width = mask.shape
        contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        boxes: list[tuple[int, int, int, int]] = []
        for contour in contours:
            x, y, box_width, box_height = cv2.boundingRect(contour)
            aspect = box_width / max(box_height, 1)
            if box_height < max(7, height * 0.28) or not 0.06 <= aspect <= 1.05:
                continue
            if box_width >= width * 0.9 or box_height >= height * 0.96:
                continue
            boxes.append((x, y, box_width, box_height))
        if not boxes:
            return None
        # 数字区域应当很紧；出现多余轮廓时优先保留最高的四个字符。
        boxes = sorted(sorted(boxes, key=lambda item: item[3], reverse=True)[:4], key=lambda item: item[0])
        digits: list[str] = []
        confidences: list[float] = []
        for x, y, box_width, box_height in boxes:
            glyph = self._normalize(mask[y:y + box_height, x:x + box_width])
            self.net.setInput(glyph.astype(np.float32)[None, None] / 255.0)
            logits = self.net.forward().reshape(-1)
            probability = np.exp(logits - logits.max())
            probability /= probability.sum()
            digits.append(str(int(probability.argmax())))
            confidences.append(float(probability.max()))
        confidence = float(np.mean(confidences))
        if confidence < float(self.config["numericMinConfidence"]):
            return None
        value = int("".join(digits))
        if value > int(self.config.get("numericMaximumValue", 9999)):
            return None
        return value, confidence

    def read(self, frame: np.ndarray) -> dict[str, Any] | None:
        frame_height, frame_width = frame.shape[:2]
        roi = self.config["numericRoi"]
        x1, y1 = round(roi[0] * frame_width), round(roi[1] * frame_height)
        x2, y2 = round(roi[2] * frame_width), round(roi[3] * frame_height)
        crop = frame[max(0, y1):min(frame_height, y2), max(0, x1):min(frame_width, x2)]
        if crop.size == 0:
            return None
        gray = cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY)
        gray = cv2.GaussianBlur(gray, (3, 3), 0)
        candidates: list[tuple[int, float]] = []
        for mode in (cv2.THRESH_BINARY, cv2.THRESH_BINARY_INV):
            _, mask = cv2.threshold(gray, 0, 255, mode | cv2.THRESH_OTSU)
            result = self._recognize_mask(mask)
            if result is not None:
                candidates.append(result)
        if not candidates:
            return None
        value, confidence = max(candidates, key=lambda item: item[1])
        result: dict[str, Any] = {
            "health": value,
            "numericConfidence": round(confidence, 3),
            "numericBox": [round(coordinate, 4) for coordinate in roi],
        }
        maximum = int(self.config.get("numericMaxHealth", 0))
        if maximum > 0:
            result["healthPercent"] = round(max(0.0, min(100.0, value / maximum * 100)), 1)
        return result


class HealthStateTracker:
    def __init__(self, config: dict[str, Any]) -> None:
        self.config = config
        self.samples: deque[float] = deque(maxlen=3)
        self.metric: str | None = None
        self.health: float | None = None
        self.low = False
        self.critical = False
        self.depleted = False
        self.missing_frames = 0

    def feed(self, detection: dict[str, Any] | None) -> list[tuple[str, dict[str, Any]]]:
        if detection is None:
            self.samples.clear()
            self.missing_frames += 1
            if self.missing_frames >= 10:
                self.health = None
                self.metric = None
                self.low = self.critical = self.depleted = False
            return []
        self.missing_frames = 0
        metric = "health" if "health" in detection else "healthPercent"
        if metric != self.metric:
            self.samples.clear()
            self.health = None
            self.metric = metric
        self.samples.append(float(detection[metric]))
        if len(self.samples) < self.samples.maxlen:
            return []
        current = float(np.median(self.samples))
        previous = self.health
        self.health = current
        if previous is None:
            current_percent = detection.get("healthPercent")
            self.low = current_percent is not None and current_percent <= float(self.config["lowThreshold"])
            self.critical = current_percent is not None and current_percent <= float(self.config["criticalThreshold"])
            self.depleted = current <= (0 if metric == "health" else 2)
            return []
        if metric == "health":
            data = {**detection, "health": round(current), "previousHealth": round(previous)}
            threshold = float(self.config.get("numericChangeThreshold", 1))
        else:
            data = {**detection, "healthPercent": round(current, 1), "previousHealthPercent": round(previous, 1)}
            threshold = float(self.config["changeThreshold"])
        events: list[tuple[str, dict[str, Any]]] = []
        change = current - previous
        change_key = "change" if metric == "health" else "changePercent"
        if change <= -threshold:
            events.append(("overwatch2.player_damaged", {**data, change_key: round(-change, 1)}))
        elif change >= threshold:
            events.append(("overwatch2.player_healed", {**data, change_key: round(change, 1)}))
        current_percent = data.get("healthPercent")
        low = current_percent is not None and current_percent <= float(self.config["lowThreshold"])
        critical = current_percent is not None and current_percent <= float(self.config["criticalThreshold"])
        depleted = current <= (0 if metric == "health" else 2)
        if low and not self.low:
            events.append(("overwatch2.health_low", data))
        if critical and not self.critical:
            events.append(("overwatch2.health_critical", data))
        if depleted and not self.depleted:
            events.append(("overwatch2.player_death", data))
        if self.depleted and current >= (1 if metric == "health" else 10):
            events.append(("overwatch2.player_recovered", data))
        self.low, self.critical, self.depleted = low, critical, depleted
        return events


class BitmapInfoHeader(ctypes.Structure):
    _fields_ = [("biSize", wintypes.DWORD), ("biWidth", wintypes.LONG), ("biHeight", wintypes.LONG),
                ("biPlanes", wintypes.WORD), ("biBitCount", wintypes.WORD), ("biCompression", wintypes.DWORD),
                ("biSizeImage", wintypes.DWORD), ("biXPelsPerMeter", wintypes.LONG), ("biYPelsPerMeter", wintypes.LONG),
                ("biClrUsed", wintypes.DWORD), ("biClrImportant", wintypes.DWORD)]


class BitmapInfo(ctypes.Structure):
    _fields_ = [("bmiHeader", BitmapInfoHeader), ("bmiColors", wintypes.DWORD * 3)]


class WindowCapture:
    SRCCOPY = 0x00CC0020
    DIB_RGB_COLORS = 0

    def __init__(self, titles: list[str]) -> None:
        self.titles = [item.casefold() for item in titles]
        user32, gdi32 = ctypes.windll.user32, ctypes.windll.gdi32
        # Win64 句柄必须声明返回类型，否则 ctypes 默认按 32 位整数截断。
        user32.GetForegroundWindow.restype = wintypes.HWND
        user32.GetDC.restype = wintypes.HDC
        gdi32.CreateCompatibleDC.restype = wintypes.HDC
        gdi32.CreateCompatibleBitmap.restype = wintypes.HBITMAP
        gdi32.SelectObject.restype = ctypes.c_void_p
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(2)
        except Exception:
            pass

    def grab_foreground(self) -> np.ndarray | None:
        user32, gdi32 = ctypes.windll.user32, ctypes.windll.gdi32
        hwnd = user32.GetForegroundWindow()
        title_length = user32.GetWindowTextLengthW(hwnd)
        title = ctypes.create_unicode_buffer(title_length + 1)
        user32.GetWindowTextW(hwnd, title, len(title))
        if not any(expected in title.value.casefold() for expected in self.titles):
            return None
        rect = wintypes.RECT()
        if not user32.GetClientRect(hwnd, ctypes.byref(rect)):
            return None
        origin = wintypes.POINT(0, 0)
        user32.ClientToScreen(hwnd, ctypes.byref(origin))
        width, height = rect.right, rect.bottom
        if width < 1280 or height < 720:
            return None
        screen_dc = user32.GetDC(0)
        memory_dc = gdi32.CreateCompatibleDC(screen_dc)
        bitmap = gdi32.CreateCompatibleBitmap(screen_dc, width, height)
        old = gdi32.SelectObject(memory_dc, bitmap)
        try:
            gdi32.BitBlt(memory_dc, 0, 0, width, height, screen_dc, origin.x, origin.y, self.SRCCOPY)
            info = BitmapInfo()
            info.bmiHeader = BitmapInfoHeader(ctypes.sizeof(BitmapInfoHeader), width, -height, 1, 32, 0, 0, 0, 0, 0, 0)
            pixels = np.empty((height, width, 4), dtype=np.uint8)
            if not gdi32.GetDIBits(memory_dc, bitmap, 0, height, pixels.ctypes.data, ctypes.byref(info), self.DIB_RGB_COLORS):
                return None
            return pixels[:, :, :3].copy()
        finally:
            gdi32.SelectObject(memory_dc, old); gdi32.DeleteObject(bitmap); gdi32.DeleteDC(memory_dc); user32.ReleaseDC(0, screen_dc)


def main() -> None:
    config = load_config()
    detector = HealthBarDetector(resource_path("healthbar_model.onnx"), config)
    numeric_reader = NumericHealthReader(resource_path("health_digit_model.onnx"), config)
    tracker = HealthStateTracker(config)
    capture = WindowCapture(config["windowTitles"])
    gateway = GatewayClient()
    session_id = f"overwatch2-{uuid.uuid4()}"
    interval = 1 / max(1, int(config["captureFps"]))
    print("守望先锋 2 血条与数字血量识别已启动；请使用无边框窗口并保持游戏在前台。", flush=True)
    while True:
        started = time.monotonic()
        gateway.maintain()
        frame = capture.grab_foreground()
        detection = detector.detect(frame) if frame is not None else None
        numeric = numeric_reader.read(frame) if frame is not None else None
        if numeric is not None:
            detection = {**(detection or {}), **numeric}
        for event_key, data in tracker.feed(detection):
            gateway.send(build_event(SOURCE, event_key, COMMANDS[event_key], data, session_id))
        time.sleep(max(0.01, interval - (time.monotonic() - started)))


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
