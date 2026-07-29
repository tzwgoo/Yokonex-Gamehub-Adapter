from __future__ import annotations

import base64
import hashlib
import json
import os
import socket
import struct
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import urlparse


LOG_PREFIX = "[YOKONEX_ISAAC] "
COMMANDS = {
    "isaac.run_started": "isaac-run-started",
    "isaac.run_ended": "isaac-run-ended",
    "isaac.floor_entered": "isaac-floor-entered",
    "isaac.room_entered": "isaac-room-entered",
    "isaac.player_damaged": "isaac-player-damaged",
    "isaac.player_healed": "isaac-player-healed",
    "isaac.player_died": "isaac-player-died",
    "isaac.player_revived": "isaac-player-revived",
    "isaac.player_joined": "isaac-player-joined",
    "isaac.character_changed": "isaac-character-changed",
    "isaac.collectible_gained": "isaac-collectible-gained",
    "isaac.collectible_lost": "isaac-collectible-lost",
    "isaac.boss_killed": "isaac-boss-killed",
}
RECONNECT_DELAYS = (1, 2, 4, 8, 15)


def log_candidates() -> list[Path]:
    configured = os.environ.get("ISAAC_LOG_PATH", "").strip()
    if configured:
        return [Path(configured).expanduser()]

    roots = [Path.home() / "Documents"]
    one_drive = os.environ.get("OneDrive", "").strip()
    if one_drive:
        roots.append(Path(one_drive) / "Documents")

    names = (
        "Binding of Isaac Repentance+",
        "Binding of Isaac Repentance",
        "Binding of Isaac Afterbirth+",
    )
    return [root / "My Games" / name / "log.txt" for root in roots for name in names]


def find_log_path() -> Path | None:
    existing = [path for path in log_candidates() if path.is_file()]
    if not existing:
        return None
    return max(existing, key=lambda path: path.stat().st_mtime)


def parse_log_line(line: str) -> dict[str, Any] | None:
    marker = line.find(LOG_PREFIX)
    if marker < 0:
        return None
    try:
        message = json.loads(line[marker + len(LOG_PREFIX) :].strip())
    except (json.JSONDecodeError, TypeError):
        return None

    event_key = str(message.get("eventKey") or "").lower()
    data = message.get("data")
    if event_key not in COMMANDS or not isinstance(data, dict):
        return None
    return {"eventKey": event_key, "data": data}


def build_event(message: dict[str, Any], session_id: str) -> dict[str, Any]:
    event_key = message["eventKey"]
    return {
        "source": "isaac",
        "eventKey": event_key,
        "commandId": COMMANDS[event_key],
        "occurredAt": datetime.now(timezone.utc).isoformat(timespec="milliseconds"),
        "eventId": str(uuid.uuid4()),
        "sessionId": session_id,
        "data": message["data"],
    }


def gateway_endpoint() -> tuple[str, int, str]:
    raw_url = os.environ.get("YOKONEX_GATEWAY_URL", "").strip()
    if raw_url:
        parsed = urlparse(raw_url)
        host = parsed.hostname or "127.0.0.1"
        port = parsed.port or 43002
        return host, port, "/v1/events"
    host = os.environ.get("YOKONEX_GATEWAY_HOST", "127.0.0.1").strip() or "127.0.0.1"
    port = int(os.environ.get("YOKONEX_GATEWAY_PORT", "43002"))
    return host, port, "/v1/events"


class EventWebSocket:
    def __init__(self, host: str, port: int, path: str) -> None:
        self.host = host
        self.port = port
        self.path = path
        self.socket: socket.socket | None = None

    def connect(self) -> None:
        self.close()
        client = socket.create_connection((self.host, self.port), timeout=1.0)
        client.settimeout(2.0)
        key = base64.b64encode(os.urandom(16)).decode("ascii")
        request = (
            f"GET {self.path} HTTP/1.1\r\n"
            f"Host: {self.host}:{self.port}\r\n"
            "Upgrade: websocket\r\n"
            "Connection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key}\r\n"
            "Sec-WebSocket-Version: 13\r\n\r\n"
        )
        client.sendall(request.encode("ascii"))
        response = self._read_http_headers(client)
        expected = base64.b64encode(
            hashlib.sha1((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode("ascii")).digest()
        ).decode("ascii")
        if not response.startswith("HTTP/1.1 101 ") or f"sec-websocket-accept: {expected}".lower() not in response.lower():
            client.close()
            raise ConnectionError("GameHub WebSocket 握手失败")
        self.socket = client

    @staticmethod
    def _read_http_headers(client: socket.socket) -> str:
        data = bytearray()
        while b"\r\n\r\n" not in data:
            chunk = client.recv(1024)
            if not chunk:
                raise ConnectionError("GameHub 在握手期间关闭连接")
            data.extend(chunk)
            if len(data) > 16 * 1024:
                raise ConnectionError("GameHub 握手响应过大")
        return data.decode("iso-8859-1")

    def send_event(self, event: dict[str, Any]) -> dict[str, Any]:
        if not self.socket:
            raise ConnectionError("WebSocket 未连接")
        payload = json.dumps(event, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        self._send_frame(0x1, payload)
        response = json.loads(self._receive_text())
        if response.get("type") != "eventResult":
            raise ConnectionError("GameHub 返回了未知消息")
        return response

    def _send_frame(self, opcode: int, payload: bytes) -> None:
        if not self.socket:
            raise ConnectionError("WebSocket 未连接")
        mask = os.urandom(4)
        length = len(payload)
        if length < 126:
            header = bytes((0x80 | opcode, 0x80 | length))
        elif length <= 0xFFFF:
            header = bytes((0x80 | opcode, 0xFE)) + struct.pack("!H", length)
        else:
            header = bytes((0x80 | opcode, 0xFF)) + struct.pack("!Q", length)
        masked = bytes(value ^ mask[index % 4] for index, value in enumerate(payload))
        self.socket.sendall(header + mask + masked)

    def _receive_text(self) -> str:
        chunks = bytearray()
        while True:
            opcode, final, payload = self._receive_frame()
            if opcode == 0x8:
                raise ConnectionError("GameHub 已关闭 WebSocket")
            if opcode == 0x9:
                self._send_frame(0xA, payload)
                continue
            if opcode in (0x0, 0x1):
                chunks.extend(payload)
                if final:
                    return chunks.decode("utf-8")

    def _receive_frame(self) -> tuple[int, bool, bytes]:
        if not self.socket:
            raise ConnectionError("WebSocket 未连接")
        first, second = self._recv_exact(2)
        final = bool(first & 0x80)
        opcode = first & 0x0F
        length = second & 0x7F
        if length == 126:
            length = struct.unpack("!H", self._recv_exact(2))[0]
        elif length == 127:
            length = struct.unpack("!Q", self._recv_exact(8))[0]
        mask = self._recv_exact(4) if second & 0x80 else None
        payload = self._recv_exact(length)
        if mask:
            payload = bytes(value ^ mask[index % 4] for index, value in enumerate(payload))
        return opcode, final, payload

    def _recv_exact(self, length: int) -> bytes:
        if not self.socket:
            raise ConnectionError("WebSocket 未连接")
        data = bytearray()
        while len(data) < length:
            chunk = self.socket.recv(length - len(data))
            if not chunk:
                raise ConnectionError("GameHub 已断开连接")
            data.extend(chunk)
        return bytes(data)

    def close(self) -> None:
        if self.socket:
            try:
                self.socket.close()
            finally:
                self.socket = None


class GatewayClient:
    def __init__(self) -> None:
        self.websocket = EventWebSocket(*gateway_endpoint())
        self.retry_index = 0
        self.next_connect_at = 0.0

    def maintain(self) -> None:
        if self.websocket.socket or time.monotonic() < self.next_connect_at:
            return
        try:
            self.websocket.connect()
            self.retry_index = 0
        except OSError:
            delay = RECONNECT_DELAYS[min(self.retry_index, len(RECONNECT_DELAYS) - 1)]
            self.retry_index += 1
            self.next_connect_at = time.monotonic() + delay

    def send(self, event: dict[str, Any]) -> bool:
        if not self.websocket.socket:
            return False
        try:
            result = self.websocket.send_event(event)
            return bool(result.get("accepted"))
        except (OSError, ConnectionError, json.JSONDecodeError):
            self.websocket.close()
            self.next_connect_at = time.monotonic() + RECONNECT_DELAYS[0]
            return False


def follow_log(path: Path):
    with path.open("r", encoding="utf-8", errors="replace") as stream:
        # 启动时从文件末尾监听，避免把旧局事件重复发送。
        stream.seek(0, os.SEEK_END)
        while True:
            line = stream.readline()
            if line:
                yield line
                continue
            try:
                if path.stat().st_size < stream.tell():
                    return
            except OSError:
                return
            yield None


def main() -> None:
    client = GatewayClient()
    session_id = f"isaac-{uuid.uuid4()}"

    while True:
        path = find_log_path()
        if not path:
            client.maintain()
            time.sleep(1)
            continue

        for line in follow_log(path):
            client.maintain()
            if line:
                message = parse_log_line(line)
                if message:
                    client.send(build_event(message, session_id))
            time.sleep(0.1)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
