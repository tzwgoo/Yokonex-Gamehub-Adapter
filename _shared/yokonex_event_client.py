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
from typing import Any, Mapping
from urllib.parse import urlparse


RECONNECT_DELAYS = (1, 2, 4, 8, 15)


def build_event(
    source: str,
    event_key: str,
    command_id: str,
    data: dict[str, Any],
    session_id: str,
) -> dict[str, Any]:
    """构造 GameHub 本机网关统一事件。"""
    return {
        "source": source,
        "eventKey": event_key,
        "commandId": command_id,
        "occurredAt": datetime.now(timezone.utc).isoformat(timespec="milliseconds"),
        "eventId": str(uuid.uuid4()),
        "sessionId": session_id,
        "data": data,
    }


def gateway_endpoint() -> tuple[str, int, str]:
    raw_url = os.environ.get("YOKONEX_GATEWAY_URL", "").strip()
    if raw_url:
        parsed = urlparse(raw_url)
        return parsed.hostname or "127.0.0.1", parsed.port or 43002, parsed.path or "/v1/events"
    host = os.environ.get("YOKONEX_GATEWAY_HOST", "127.0.0.1").strip() or "127.0.0.1"
    port = int(os.environ.get("YOKONEX_GATEWAY_PORT", "43002"))
    return host, port, "/v1/events"


class EventWebSocket:
    """只实现本机事件网关所需的最小 WebSocket 客户端，避免额外依赖。"""

    def __init__(
        self,
        host: str,
        port: int,
        path: str,
        headers: Mapping[str, str] | None = None,
    ) -> None:
        self.host = host
        self.port = port
        self.path = path
        self.headers = dict(headers or {})
        self.socket: socket.socket | None = None

    def connect(self) -> None:
        self.close()
        client = socket.create_connection((self.host, self.port), timeout=1.0)
        client.settimeout(2.0)
        key = base64.b64encode(os.urandom(16)).decode("ascii")
        extra_headers = "".join(
            f"{name}: {value}\r\n"
            for name, value in self.headers.items()
            if "\r" not in name and "\n" not in name and "\r" not in value and "\n" not in value
        )
        request = (
            f"GET {self.path} HTTP/1.1\r\nHost: {self.host}:{self.port}\r\n"
            "Upgrade: websocket\r\nConnection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n"
            f"{extra_headers}\r\n"
        )
        client.sendall(request.encode("ascii"))
        response = self._read_headers(client)
        expected = base64.b64encode(
            hashlib.sha1((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode()).digest()
        ).decode()
        if not response.startswith("HTTP/1.1 101 ") or expected.lower() not in response.lower():
            client.close()
            raise ConnectionError("GameHub WebSocket 握手失败")
        self.socket = client

    @staticmethod
    def _read_headers(client: socket.socket) -> str:
        data = bytearray()
        while b"\r\n\r\n" not in data:
            chunk = client.recv(1024)
            if not chunk:
                raise ConnectionError("GameHub 在握手期间关闭连接")
            data.extend(chunk)
            if len(data) > 16 * 1024:
                raise ConnectionError("GameHub 握手响应过大")
        return data.decode("iso-8859-1")

    def send(self, event: dict[str, Any]) -> dict[str, Any]:
        payload = json.dumps(event, ensure_ascii=False, separators=(",", ":")).encode()
        self._send_frame(0x1, payload)
        return json.loads(self._receive_text())

    def receive_json(self) -> dict[str, Any]:
        """接收只推送事件的本机 WebSocket 消息。"""
        return json.loads(self._receive_text())

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
            elif opcode in (0x0, 0x1):
                chunks.extend(payload)
                if final:
                    return chunks.decode()

    def _receive_frame(self) -> tuple[int, bool, bytes]:
        first, second = self._recv_exact(2)
        length = second & 0x7F
        if length == 126:
            length = struct.unpack("!H", self._recv_exact(2))[0]
        elif length == 127:
            length = struct.unpack("!Q", self._recv_exact(8))[0]
        mask = self._recv_exact(4) if second & 0x80 else None
        payload = self._recv_exact(length)
        if mask:
            payload = bytes(value ^ mask[index % 4] for index, value in enumerate(payload))
        return first & 0x0F, bool(first & 0x80), payload

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
            self.socket.close()
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
        self.maintain()
        if not self.websocket.socket:
            return False
        try:
            result = self.websocket.send(event)
            return result.get("type") == "eventResult" and bool(result.get("accepted"))
        except (OSError, ConnectionError, json.JSONDecodeError):
            self.websocket.close()
            self.next_connect_at = time.monotonic() + RECONNECT_DELAYS[0]
            return False
