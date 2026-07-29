from __future__ import annotations

import json
import socket
import sys
import uuid
from pathlib import Path
from typing import Any

try:
    from yokonex_event_client import GatewayClient, build_event
except ModuleNotFoundError:
    # 仓库内直接调试时，共享模块位于相邻的 _shared 目录。
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "_shared"))
    from yokonex_event_client import GatewayClient, build_event


SOURCE = "factorio"
UDP_PORT = 34198
COMMANDS = {
    "factorio.player_damaged": "factorio-player-damaged",
    "factorio.player_died": "factorio-player-died",
    "factorio.player_respawned": "factorio-player-respawned",
    "factorio.entity_built": "factorio-entity-built",
    "factorio.entity_mined": "factorio-entity-mined",
    "factorio.research_finished": "factorio-research-finished",
    "factorio.achievement_unlocked": "factorio-achievement-unlocked",
    "factorio.rocket_launched": "factorio-rocket-launched",
    "factorio.train_state_changed": "factorio-train-state-changed",
}


def parse_packet(packet: bytes) -> tuple[str, dict[str, Any]] | None:
    try:
        message = json.loads(packet.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return None
    event_key = str(message.get("eventKey") or "").lower()
    data = message.get("data")
    if event_key not in COMMANDS or not isinstance(data, dict):
        return None
    return event_key, data


def main() -> None:
    client = GatewayClient()
    session_id = f"factorio-{uuid.uuid4()}"
    listener = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    # 只监听回环地址，游戏事件不会暴露到局域网。
    listener.bind(("127.0.0.1", UDP_PORT))
    listener.settimeout(0.5)
    while True:
        client.maintain()
        try:
            packet, _ = listener.recvfrom(65507)
        except TimeoutError:
            continue
        parsed = parse_packet(packet)
        if parsed:
            event_key, data = parsed
            client.send(build_event(SOURCE, event_key, COMMANDS[event_key], data, session_id))


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
